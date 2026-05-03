using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Nexus.API.Models;
using Nexus.API.Validation;
using Xunit;

namespace Nexus.API.Tests.Validation;

public class UploadCsvRequestValidatorTests
{
    [Fact]
    public void Validate_WithValidRequest_ReturnsValid()
    {
        var validator = new UploadCsvRequestValidator();
        var request = new UploadCsvRequest
        {
            File = CreateFormFile("test.csv"),
            CarrierCode = "CARRIER"
        };

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithUnsupportedExtension_ReturnsInvalid()
    {
        var validator = new UploadCsvRequestValidator();
        var request = new UploadCsvRequest
        {
            File = CreateFormFile("test.txt"),
            CarrierCode = "CARRIER"
        };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithMissingCarrierCode_ReturnsInvalid()
    {
        var validator = new UploadCsvRequestValidator();
        var request = new UploadCsvRequest
        {
            File = CreateFormFile("test.csv"),
            CarrierCode = string.Empty
        };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
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
}
