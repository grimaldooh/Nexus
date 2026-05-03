using Microsoft.AspNetCore.Http;

namespace Nexus.API.Models;

public class UploadCsvRequest
{
    public IFormFile File { get; set; } = default!;
    public string? SourceName { get; set; }
    public string CarrierCode { get; set; } = string.Empty;
}
