using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Infrastructure.Data;

namespace Nexus.API.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly NexusDbContext _dbContext;

    public HealthController(NexusDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Health check endpoint that tests database connectivity.
    /// No API key required for this endpoint.
    /// </summary>
    /// <response code="200">Database is accessible.</response>
    /// <response code="500">Database connection failed.</response>
    [HttpGet("database")]
    [ProducesResponseType(typeof(DatabaseHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CheckDatabaseHealth(CancellationToken cancellationToken)
    {
        try
        {
            // Test database connectivity by counting batches
            var batchCount = await _dbContext.Batches.CountAsync(cancellationToken);
            var transactionCount = await _dbContext.InsuranceTransactions.CountAsync(cancellationToken);

            return Ok(new DatabaseHealthResponse
            {
                Status = "Healthy",
                Message = "Database connection successful",
                Timestamp = DateTime.UtcNow,
                DatabaseMetrics = new
                {
                    TotalBatches = batchCount,
                    TotalTransactions = transactionCount
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Status = "Unhealthy",
                Message = $"Database connection failed: {ex.Message}",
                Timestamp = DateTime.UtcNow,
                ErrorDetails = ex.GetType().Name
            });
        }
    }

    /// <summary>
    /// Quick health check for API availability (no database query).
    /// </summary>
    /// <response code="200">API is running.</response>
    [HttpGet("ping")]
    [ProducesResponseType(typeof(PingResponse), StatusCodes.Status200OK)]
    public IActionResult Ping()
    {
        return Ok(new PingResponse
        {
            Status = "OK",
            Timestamp = DateTime.UtcNow,
            Message = "Nexus API is running"
        });
    }
}

public class DatabaseHealthResponse
{
    public string Status { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
    public object DatabaseMetrics { get; set; }
}

public class ErrorResponse
{
    public string Status { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
    public string ErrorDetails { get; set; }
}

public class PingResponse
{
    public string Status { get; set; }
    public DateTime Timestamp { get; set; }
    public string Message { get; set; }
}
