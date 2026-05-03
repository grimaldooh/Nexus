using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Application.DTOs;
using Nexus.Application.Interfaces;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Domain.Interfaces;
using Nexus.Infrastructure.Data;
using Nexus.Infrastructure.Ingestion;
using Xunit;

namespace Nexus.Infrastructure.Tests.Ingestion;

public class IngestionPipelineTests
{
    [Fact]
    public async Task StageAsync_WithStrategy_StagesTransactions()
    {
        await using var dbContext = CreateDbContext();

        var mappingMock = new Mock<ICarrierMappingService>();
        mappingMock
            .Setup(x => x.MapAsync(It.IsAny<Guid>(), It.IsAny<RawCarrierRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid batchId, RawCarrierRecord record, CancellationToken _) => new InsuranceTransaction
            {
                Id = Guid.NewGuid(),
                BatchId = batchId,
                ExternalId = "E-1",
                PolicyNumber = "P-1",
                CarrierCode = record.CarrierCode,
                Status = TransactionStatus.Pending,
                TransactionDate = DateTime.UtcNow
            });

        var piiMock = new Mock<IPiiMaskingService>();
        piiMock
            .Setup(x => x.Mask(It.IsAny<InsuranceTransaction>()))
            .Returns<InsuranceTransaction>(transaction =>
            {
                transaction.PIIMasked = true;
                return transaction;
            });

        var strategies = new List<IIngestionStrategy>
        {
            new FakeStrategy()
        };

        var loggerMock = new Mock<ILogger<IngestionPipeline>>();
        var pipeline = new IngestionPipeline(strategies, mappingMock.Object, piiMock.Object, dbContext, loggerMock.Object);

        var batchId = Guid.NewGuid();
        await pipeline.StageAsync(batchId, "file.csv", "ABC", CancellationToken.None);

        var transactions = await dbContext.InsuranceTransactions.ToListAsync();
        transactions.Should().HaveCount(2);
        transactions.Should().OnlyContain(x => x.PIIMasked);
    }

    [Fact]
    public async Task StageAsync_WithoutStrategy_Throws()
    {
        await using var dbContext = CreateDbContext();
        var loggerMock = new Mock<ILogger<IngestionPipeline>>();
        var pipeline = new IngestionPipeline(new List<IIngestionStrategy>(),
            Mock.Of<ICarrierMappingService>(),
            Mock.Of<IPiiMaskingService>(),
            dbContext,
            loggerMock.Object);

        var action = async () => await pipeline.StageAsync(Guid.NewGuid(), "file.csv", "ABC", CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    private static NexusDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new NexusDbContext(options);
    }

    private sealed class FakeStrategy : IIngestionStrategy
    {
        public bool CanHandle(string fileExtension)
        {
            return fileExtension.Equals(".csv", StringComparison.OrdinalIgnoreCase);
        }

        public async IAsyncEnumerable<RawCarrierRecord> ReadAsync(
            string filePath,
            string carrierCode,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new RawCarrierRecord(carrierCode, new Dictionary<string, string?>());
            yield return new RawCarrierRecord(carrierCode, new Dictionary<string, string?>());
            await Task.CompletedTask;
        }
    }
}
