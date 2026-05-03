using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.API.Controllers;
using Nexus.Application.DTOs;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Data;
using Xunit;

namespace Nexus.API.Tests.Controllers;

public class BatchesControllerTests
{
    [Fact]
    public async Task GetStatus_WithMissingBatch_ReturnsNotFound()
    {
        await using var dbContext = CreateDbContext();
        var controller = new BatchesController(dbContext);

        var result = await controller.GetStatus(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetStatus_WithBatch_ReturnsCounts()
    {
        var batchId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();

        dbContext.Batches.Add(new Batch
        {
            Id = batchId,
            SourceName = "Carrier-A",
            UploadDate = DateTime.UtcNow,
            Status = BatchStatus.Processing
        });

        dbContext.InsuranceTransactions.AddRange(
            new InsuranceTransaction
            {
                Id = Guid.NewGuid(),
                BatchId = batchId,
                ExternalId = "E-1",
                PolicyNumber = "P-1",
                CarrierCode = "C-1",
                Status = TransactionStatus.Clean,
                TransactionDate = DateTime.UtcNow
            },
            new InsuranceTransaction
            {
                Id = Guid.NewGuid(),
                BatchId = batchId,
                ExternalId = "E-2",
                PolicyNumber = "P-2",
                CarrierCode = "C-1",
                Status = TransactionStatus.Duplicate,
                TransactionDate = DateTime.UtcNow
            },
            new InsuranceTransaction
            {
                Id = Guid.NewGuid(),
                BatchId = batchId,
                ExternalId = "E-3",
                PolicyNumber = "P-3",
                CarrierCode = "C-1",
                Status = TransactionStatus.Suspect,
                TransactionDate = DateTime.UtcNow
            });

        await dbContext.SaveChangesAsync();

        var controller = new BatchesController(dbContext);

        var result = await controller.GetStatus(batchId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<BatchStatusDto>().Subject;

        dto.BatchId.Should().Be(batchId);
        dto.TotalRecords.Should().Be(3);
        dto.CleanCount.Should().Be(1);
        dto.DuplicateCount.Should().Be(1);
        dto.SuspectCount.Should().Be(1);
    }

    private static NexusDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new NexusDbContext(options);
    }
}
