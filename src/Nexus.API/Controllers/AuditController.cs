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

    /// <summary>
    /// Returns transactions flagged for manual review.
    /// </summary>
    /// <response code="200">Suspect transactions returned.</response>
    /// <response code="401">Missing or invalid API key.</response>
    [HttpGet("suspects")]
    [ProducesResponseType(typeof(IEnumerable<AuditSuspectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSuspects(CancellationToken cancellationToken)
    {
        var suspects = await _dbContext.InsuranceTransactions
            .AsNoTracking()
            .Where(x => x.Status == TransactionStatus.Suspect)
            .Select(x => new AuditSuspectDto
            {
                TransactionId = x.Id,
                PolicyNumber = x.PolicyNumber,
                NetCommission = x.NetCommission,
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

    /// <summary>
    /// Resolves a suspect transaction as approved or invalid.
    /// </summary>
    /// <response code="200">Resolution accepted.</response>
    /// <response code="400">Invalid request payload.</response>
    /// <response code="401">Missing or invalid API key.</response>
    /// <response code="404">Transaction not found.</response>
    [HttpPost("resolve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve([FromBody] ResolveAuditRequest request, CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.InsuranceTransactions
            .FirstOrDefaultAsync(x => x.Id == request.TransactionId, cancellationToken);

        if (transaction is null)
        {
            return NotFound();
        }

        transaction.Status = request.Approve ? TransactionStatus.Clean : TransactionStatus.Invalid;

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
