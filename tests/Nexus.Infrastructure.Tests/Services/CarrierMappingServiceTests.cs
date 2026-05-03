using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Nexus.Application.DTOs;
using Nexus.Application.Interfaces;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Data;
using Nexus.Infrastructure.Services;
using Xunit;

namespace Nexus.Infrastructure.Tests.Services;

public class CarrierMappingServiceTests
{
    [Fact]
    public async Task MapAsync_WithMappings_ReturnsPendingTransaction()
    {
        await using var dbContext = CreateDbContext();
        dbContext.CarrierMappings.AddRange(
            new CarrierMapping
            {
                Id = Guid.NewGuid(),
                CarrierCode = "ABC",
                SourceField = "external_id",
                TargetField = "ExternalId",
                IsRequired = true
            },
            new CarrierMapping
            {
                Id = Guid.NewGuid(),
                CarrierCode = "ABC",
                SourceField = "policy",
                TargetField = "PolicyNumber",
                IsRequired = true
            },
            new CarrierMapping
            {
                Id = Guid.NewGuid(),
                CarrierCode = "ABC",
                SourceField = "net_commission",
                TargetField = "NetCommission"
            },
            new CarrierMapping
            {
                Id = Guid.NewGuid(),
                CarrierCode = "ABC",
                SourceField = "txn_date",
                TargetField = "TransactionDate"
            });

        await dbContext.SaveChangesAsync();

        var aiMock = new Mock<IAIIntegrityService>();
        var loggerMock = new Mock<ILogger<CarrierMappingService>>();
        var service = new CarrierMappingService(dbContext, aiMock.Object, loggerMock.Object);

        var record = new RawCarrierRecord("ABC", new Dictionary<string, string?>
        {
            ["external_id"] = "E-1",
            ["policy"] = "P-1",
            ["net_commission"] = "120.50",
            ["txn_date"] = "2024-01-01"
        });

        var transaction = await service.MapAsync(Guid.NewGuid(), record, CancellationToken.None);

        transaction.Status.Should().Be(TransactionStatus.Pending);
        transaction.ExternalId.Should().Be("E-1");
        transaction.PolicyNumber.Should().Be("P-1");
        transaction.NetCommission.Should().Be(120.50m);
        transaction.TransactionDate.Date.Should().Be(new DateTime(2024, 1, 1));
    }

    [Fact]
    public async Task MapAsync_WithMissingRequiredFields_ReturnsInvalid()
    {
        await using var dbContext = CreateDbContext();
        dbContext.CarrierMappings.Add(new CarrierMapping
        {
            Id = Guid.NewGuid(),
            CarrierCode = "ABC",
            SourceField = "policy",
            TargetField = "PolicyNumber",
            IsRequired = true
        });

        await dbContext.SaveChangesAsync();

        var aiMock = new Mock<IAIIntegrityService>();
        var loggerMock = new Mock<ILogger<CarrierMappingService>>();
        var service = new CarrierMappingService(dbContext, aiMock.Object, loggerMock.Object);

        var record = new RawCarrierRecord("ABC", new Dictionary<string, string?>());

        var transaction = await service.MapAsync(Guid.NewGuid(), record, CancellationToken.None);

        transaction.Status.Should().Be(TransactionStatus.Invalid);
    }

    private static NexusDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new NexusDbContext(options);
    }
}
