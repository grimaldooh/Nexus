using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Application.DTOs;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Data;

namespace Nexus.API.Controllers;

[ApiController]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private readonly NexusDbContext _dbContext;

    public AuditController(NexusDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("suspects")]
    [ProducesResponseType(typeof(IEnumerable<AuditSuspectDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuspects(CancellationToken cancellationToken)
    {
        var suspects = await _dbContext.InsuranceTransactions
            .AsNoTracking()
            .Where(x => x.Status == TransactionStatus.Suspect)
            .Select(x => new AuditSuspectDto
            {
                TransactionId = x.Id,
                PolicyNumber = x.PolicyNumber,
                Amount = x.Amount,
                TransactionDate = x.TransactionDate,
                ConfidenceScore = x.ConfidenceScore,
                Notes = x.Notes,
                Reason = x.SanitizationLogs
                    .OrderByDescending(log => log.CreatedAt)
                    .Select(log => log.Reason)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return Ok(suspects);
    }

    [HttpPost("resolve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve([FromBody] ResolveAuditRequest request, CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.InsuranceTransactions
            .FirstOrDefaultAsync(x => x.Id == request.TransactionId, cancellationToken);

        if (transaction is null)
        {
            return NotFound();
        }

        transaction.Status = request.Approve ? TransactionStatus.Clean : TransactionStatus.Rejected;

        _dbContext.SanitizationLogs.Add(new Nexus.Domain.Entities.SanitizationLog
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            DecisionType = SanitizationDecisionType.Manual,
            Reason = request.Reason,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}
