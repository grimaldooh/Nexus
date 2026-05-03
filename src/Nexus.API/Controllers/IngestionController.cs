using Microsoft.AspNetCore.Mvc;
using Nexus.API.Background;
using Nexus.API.Models;
using Nexus.Application.DTOs;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Data;

namespace Nexus.API.Controllers;

[ApiController]
[Route("api/ingestion")]
public class IngestionController : ControllerBase
{
    private readonly NexusDbContext _dbContext;
    private readonly IIngestionQueue _queue;
    private readonly IWebHostEnvironment _environment;

    public IngestionController(NexusDbContext dbContext, IIngestionQueue queue, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _queue = queue;
        _environment = environment;
    }

    [HttpPost("upload")]
    [ProducesResponseType(typeof(UploadResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upload([FromForm] UploadCsvRequest request, CancellationToken cancellationToken)
    {
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            SourceName = request.SourceName ?? request.File.FileName,
            UploadDate = DateTime.UtcNow,
            Status = BatchStatus.Pending
        };

        _dbContext.Batches.Add(batch);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var uploadDirectory = Path.Combine(_environment.ContentRootPath, "App_Data", "ingestion");
        Directory.CreateDirectory(uploadDirectory);

        var fileExtension = Path.GetExtension(request.File.FileName);
        var filePath = Path.Combine(uploadDirectory, $"{batch.Id}{fileExtension}");
        await using (var fileStream = System.IO.File.Create(filePath))
        {
            await request.File.CopyToAsync(fileStream, cancellationToken);
        }

            await _queue.QueueAsync(new IngestionJob(batch.Id, filePath, batch.SourceName, request.CarrierCode), cancellationToken);

        return Ok(new UploadResultDto { BatchId = batch.Id });
    }
}
