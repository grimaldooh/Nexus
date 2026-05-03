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

public class AuditControllerTests
{
    [Fact]
    public async Task GetSuspects_ReturnsOnlySuspectsWithLatestReason()
    {
        var batchId = Guid.NewGuid();
        var suspectId = Guid.NewGuid();

        await using var dbContext = CreateDbContext();

        var suspect = new InsuranceTransaction
        {
            Id = suspectId,
            BatchId = batchId,
            ExternalId = "E-1",
            PolicyNumber = "P-1",
            CarrierCode = "C-1",
            Status = TransactionStatus.Suspect,
            TransactionDate = DateTime.UtcNow
        };

        var latestLog = new SanitizationLog
        {
            Id = Guid.NewGuid(),
            TransactionId = suspectId,
            DecisionType = SanitizationDecisionType.Auto,
            Reason = "Latest reason",
            CreatedAt = DateTime.UtcNow.AddMinutes(10)
        };

        var olderLog = new SanitizationLog
        {
            Id = Guid.NewGuid(),
            TransactionId = suspectId,
            DecisionType = SanitizationDecisionType.Auto,
            Reason = "Older reason",
            CreatedAt = DateTime.UtcNow
        };

        suspect.SanitizationLogs.Add(olderLog);
        suspect.SanitizationLogs.Add(latestLog);

        dbContext.InsuranceTransactions.Add(suspect);
        dbContext.SanitizationLogs.AddRange(olderLog, latestLog);

        dbContext.InsuranceTransactions.Add(new InsuranceTransaction
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            ExternalId = "E-2",
            PolicyNumber = "P-2",
            CarrierCode = "C-1",
            Status = TransactionStatus.Clean,
            TransactionDate = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var controller = new AuditController(dbContext);

        var result = await controller.GetSuspects(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var suspects = okResult.Value.Should().BeAssignableTo<IEnumerable<AuditSuspectDto>>().Subject.ToList();

        suspects.Should().HaveCount(1);
        suspects[0].TransactionId.Should().Be(suspectId);
        suspects[0].Reason.Should().Be("Latest reason");
    }

    [Fact]
    public async Task Resolve_WhenMissingTransaction_ReturnsNotFound()
    {
        await using var dbContext = CreateDbContext();
        var controller = new AuditController(dbContext);

        var request = new ResolveAuditRequest
        {
            TransactionId = Guid.NewGuid(),
            Approve = true,
            Reason = "ok"
        };

        var result = await controller.Resolve(request, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Resolve_WhenApproved_UpdatesStatusAndCreatesLog()
    {
        var transactionId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();

        dbContext.InsuranceTransactions.Add(new InsuranceTransaction
        {
            Id = transactionId,
            BatchId = Guid.NewGuid(),
            ExternalId = "E-1",
            PolicyNumber = "P-1",
            CarrierCode = "C-1",
            Status = TransactionStatus.Suspect,
            TransactionDate = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var controller = new AuditController(dbContext);

        var request = new ResolveAuditRequest
        {
            TransactionId = transactionId,
            Approve = true,
            Reason = "Approved"
        };

        var result = await controller.Resolve(request, CancellationToken.None);

        result.Should().BeOfType<OkResult>();

        var updated = await dbContext.InsuranceTransactions.FirstAsync(x => x.Id == transactionId);
        updated.Status.Should().Be(TransactionStatus.Clean);

        var logs = await dbContext.SanitizationLogs.Where(x => x.TransactionId == transactionId).ToListAsync();
        logs.Should().HaveCount(1);
        logs[0].DecisionType.Should().Be(SanitizationDecisionType.Manual);
    }

    private static NexusDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new NexusDbContext(options);
    }
}
