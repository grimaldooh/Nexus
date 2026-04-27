using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Application.DTOs;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Data;

namespace Nexus.API.Controllers;

[ApiController]
[Route("api/batches")]
public class BatchesController : ControllerBase
{
    private readonly NexusDbContext _dbContext;

    public BatchesController(NexusDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BatchStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken cancellationToken)
    {
        var batch = await _dbContext.Batches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (batch is null)
        {
            return NotFound();
        }

        var transactions = await _dbContext.InsuranceTransactions
            .AsNoTracking()
            .Where(x => x.BatchId == id)
            .ToListAsync(cancellationToken);

        var dto = new BatchStatusDto
        {
            BatchId = batch.Id,
            Status = batch.Status,
            TotalRecords = transactions.Count,
            CleanCount = transactions.Count(x => x.Status == TransactionStatus.Clean),
            DuplicateCount = transactions.Count(x => x.Status == TransactionStatus.Duplicate),
            SuspectCount = transactions.Count(x => x.Status == TransactionStatus.Suspect)
        };

        return Ok(dto);
    }
}
