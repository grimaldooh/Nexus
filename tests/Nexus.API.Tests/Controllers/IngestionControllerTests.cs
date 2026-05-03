using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Nexus.API.Background;
using Nexus.API.Controllers;
using Nexus.API.Models;
using Nexus.Application.DTOs;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Data;
using Xunit;

namespace Nexus.API.Tests.Controllers;

public class IngestionControllerTests
{
    [Fact]
    public async Task Upload_QueuesJobAndSavesBatch()
    {
        await using var dbContext = CreateDbContext();

        var queueMock = new Mock<IIngestionQueue>();
        IngestionJob? capturedJob = null;

        queueMock
            .Setup(x => x.QueueAsync(It.IsAny<IngestionJob>(), It.IsAny<CancellationToken>()))
            .Callback<IngestionJob, CancellationToken>((job, _) => capturedJob = job)
            .Returns(new ValueTask());

        var environmentMock = new Mock<IWebHostEnvironment>();
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        environmentMock.Setup(x => x.ContentRootPath).Returns(rootPath);

        var controller = new IngestionController(dbContext, queueMock.Object, environmentMock.Object);

        var request = new UploadCsvRequest
        {
            File = CreateFormFile("upload.csv"),
            SourceName = "Carrier-Upload",
            CarrierCode = "CARRIER"
        };

        var result = await controller.Upload(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<UploadResultDto>().Subject;

        payload.BatchId.Should().NotBe(Guid.Empty);

        var batch = await dbContext.Batches.FirstAsync(x => x.Id == payload.BatchId);
        batch.Status.Should().Be(BatchStatus.Pending);
        batch.SourceName.Should().Be("Carrier-Upload");

        capturedJob.Should().NotBeNull();
        capturedJob!.BatchId.Should().Be(payload.BatchId);
        capturedJob!.CarrierCode.Should().Be("CARRIER");
        capturedJob!.SourceName.Should().Be("Carrier-Upload");
        File.Exists(capturedJob.FilePath).Should().BeTrue();
    }

    private static IFormFile CreateFormFile(string fileName)
    {
        var content = Encoding.UTF8.GetBytes("a,b\n1,2");
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
    }

    private static NexusDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new NexusDbContext(options);
    }
}
