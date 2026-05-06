using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nexus.Application.DTOs;
using Nexus.Domain.Entities;
using Nexus.Infrastructure.Data;
using Nexus.Infrastructure.Options;
using Nexus.Infrastructure.Services;
using Xunit;

namespace Nexus.Infrastructure.Tests.Services;

public class AiIntegrityServiceTests
{
    private readonly Mock<HttpClient> _mockHttpClient;
    private readonly NexusDbContext _dbContext;
    private readonly Mock<IOptions<OpenAiOptions>> _mockOptions;
    private readonly Mock<ILogger<AiIntegrityService>> _mockLogger;
    private readonly AiIntegrityService _service;

    public AiIntegrityServiceTests()
    {
        _mockHttpClient = new Mock<HttpClient>();
        
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_" + Guid.NewGuid())
            .Options;
        _dbContext = new NexusDbContext(options);
        
        _mockOptions = new Mock<IOptions<OpenAiOptions>>();
        _mockLogger = new Mock<ILogger<AiIntegrityService>>();

        _mockOptions.Setup(x => x.Value).Returns(new OpenAiOptions
        {
            ApiKey = "test-api-key-12345",
            Model = "gpt-4o-mini"
        });

        _service = new AiIntegrityService(
            _mockHttpClient.Object,
            _dbContext,
            _mockOptions.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task TryMapUnknownAsync_WithValidCsvHeaders_ReturnsMappedRecord()
    {
        // Arrange
        var record = new RawCarrierRecord(
            "TEST-CARRIER",
            new Dictionary<string, string?>
            {
                { "poliza_numero", "POL-001" },
                { "comisión_neta", "500.00" },
                { "fecha_vigencia", "2024-05-03" }
            }
        );

        var openAiResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = @"[
                            { ""sourceField"": ""poliza_numero"", ""targetField"": ""PolicyNumber"" },
                            { ""sourceField"": ""comisión_neta"", ""targetField"": ""NetCommission"" },
                            { ""sourceField"": ""fecha_vigencia"", ""targetField"": ""TransactionDate"" }
                        ]"
                    }
                }
            }
        };

        var responseContent = System.Text.Json.JsonSerializer.Serialize(openAiResponse);
        var httpResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpClient
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.TryMapUnknownAsync(record, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result?.CarrierCode.Should().Be("TEST-CARRIER");
        var savedMappings = _dbContext.CarrierMappings.ToList();
        savedMappings.Should().HaveCount(3);
    }

    [Fact]
    public async Task TryMapUnknownAsync_WithMissingApiKey_ReturnsNull()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_" + Guid.NewGuid())
            .Options;
        var dbContext = new NexusDbContext(options);
        var mockOptions = new Mock<IOptions<OpenAiOptions>>();
        mockOptions.Setup(x => x.Value).Returns(new OpenAiOptions
        {
            ApiKey = string.Empty,
            Model = "gpt-4o-mini"
        });

        var service = new AiIntegrityService(
            _mockHttpClient.Object,
            dbContext,
            mockOptions.Object,
            _mockLogger.Object);

        var record = new RawCarrierRecord(
            "TEST",
            new Dictionary<string, string?> { { "header1", "value1" } }
        );

        // Act
        var result = await service.TryMapUnknownAsync(record, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task TryMapUnknownAsync_WithPlaceholderApiKey_ReturnsNull()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_" + Guid.NewGuid())
            .Options;
        var dbContext = new NexusDbContext(options);
        var mockOptions = new Mock<IOptions<OpenAiOptions>>();
        mockOptions.Setup(x => x.Value).Returns(new OpenAiOptions
        {
            ApiKey = "your-api-key-here",
            Model = "gpt-4o-mini"
        });

        var service = new AiIntegrityService(
            _mockHttpClient.Object,
            dbContext,
            mockOptions.Object,
            _mockLogger.Object);

        var record = new RawCarrierRecord(
            "TEST",
            new Dictionary<string, string?>()
        );

        // Act
        var result = await service.TryMapUnknownAsync(record, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task TryMapUnknownAsync_WithEmptyHeaders_ReturnsNull()
    {
        // Arrange
        var record = new RawCarrierRecord(
            "TEST",
            new Dictionary<string, string?>()
        );

        // Act
        var result = await _service.TryMapUnknownAsync(record, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task TryMapUnknownAsync_WithOpenAiHttpError_ReturnsNull()
    {
        // Arrange
        var record = new RawCarrierRecord(
            "TEST-CARRIER",
            new Dictionary<string, string?>
            {
                { "header1", "value1" },
                { "header2", "value2" }
            }
        );

        var httpResponse = new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("Invalid API key", System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpClient
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.TryMapUnknownAsync(record, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TryMapUnknownAsync_WithHttpException_ReturnsNull()
    {
        // Arrange
        var record = new RawCarrierRecord(
            "TEST-CARRIER",
            new Dictionary<string, string?>
            {
                { "header1", "value1" }
            }
        );

        _mockHttpClient
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _service.TryMapUnknownAsync(record, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task TryMapUnknownAsync_WithInvalidJsonResponse_ReturnsNull()
    {
        // Arrange
        var record = new RawCarrierRecord(
            "TEST-CARRIER",
            new Dictionary<string, string?>
            {
                { "header1", "value1" }
            }
        );

        var openAiResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = "This is not valid JSON at all"
                    }
                }
            }
        };

        var responseContent = System.Text.Json.JsonSerializer.Serialize(openAiResponse);
        var httpResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpClient
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.TryMapUnknownAsync(record, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task TryMapUnknownAsync_WithEmptyMappings_ReturnsNull()
    {
        // Arrange
        var record = new RawCarrierRecord(
            "TEST-CARRIER",
            new Dictionary<string, string?>
            {
                { "header1", "value1" }
            }
        );

        var openAiResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = "[]"
                    }
                }
            }
        };

        var responseContent = System.Text.Json.JsonSerializer.Serialize(openAiResponse);
        var httpResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpClient
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.TryMapUnknownAsync(record, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task TryMapUnknownAsync_WithComplexHeaders_SuccessfullyMapsAllFields()
    {
        // Arrange
        var record = new RawCarrierRecord(
            "COMPLEX-CARRIER",
            new Dictionary<string, string?>
            {
                { "id_externo", "EXT-123" },
                { "poliza", "POL-456" },
                { "prima_bruta", "5000.00" },
                { "comisión", "500.00" },
                { "fecha_efectiva", "2024-05-03" },
                { "observaciones", "Renovación especial" },
                { "agent_name", "John Doe" },
                { "region_code", "US-CA" }
            }
        );

        var openAiResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = @"[
                            { ""sourceField"": ""id_externo"", ""targetField"": ""ExternalId"" },
                            { ""sourceField"": ""poliza"", ""targetField"": ""PolicyNumber"" },
                            { ""sourceField"": ""prima_bruta"", ""targetField"": ""GrossPremium"" },
                            { ""sourceField"": ""comisión"", ""targetField"": ""NetCommission"" },
                            { ""sourceField"": ""fecha_efectiva"", ""targetField"": ""TransactionDate"" },
                            { ""sourceField"": ""observaciones"", ""targetField"": ""Notes"" }
                        ]"
                    }
                }
            }
        };

        var responseContent = System.Text.Json.JsonSerializer.Serialize(openAiResponse);
        var httpResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpClient
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.TryMapUnknownAsync(record, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result?.CarrierCode.Should().Be("COMPLEX-CARRIER");
        var mappings = _dbContext.CarrierMappings.ToList();
        mappings.Should().HaveCount(6);
    }

    [Fact]
    public async Task TryMapUnknownAsync_WithJsonMarkdownFormatted_StripsMarkdownAndParsesSuccessfully()
    {
        // Arrange
        var record = new RawCarrierRecord(
            "TEST-CARRIER",
            new Dictionary<string, string?>
            {
                { "policy_number", "POL-001" },
                { "commission_amt", "500.00" }
            }
        );

        var openAiResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = @"```json
[
  { ""sourceField"": ""policy_number"", ""targetField"": ""PolicyNumber"" },
  { ""sourceField"": ""commission_amt"", ""targetField"": ""NetCommission"" }
]
```"
                    }
                }
            }
        };

        var responseContent = System.Text.Json.JsonSerializer.Serialize(openAiResponse);
        var httpResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpClient
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(httpResponse);

        // Using real DbContext

        // Act
        var result = await _service.TryMapUnknownAsync(record, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var savedMappings = _dbContext.CarrierMappings.ToList();
        savedMappings.Should().HaveCount(2);
    }

    [Fact]
    public async Task TryMapUnknownAsync_MarksPolicyNumberAndExternalIdAsRequired()
    {
        // Arrange
        var record = new RawCarrierRecord(
            "TEST-CARRIER",
            new Dictionary<string, string?>
            {
                { "id", "TST-001" },
                { "policy", "POL-001" },
                { "commission", "500.00" }
            }
        );

        var openAiResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = @"[
                            { ""sourceField"": ""id"", ""targetField"": ""ExternalId"" },
                            { ""sourceField"": ""policy"", ""targetField"": ""PolicyNumber"" },
                            { ""sourceField"": ""commission"", ""targetField"": ""NetCommission"" }
                        ]"
                    }
                }
            }
        };

        var responseContent = System.Text.Json.JsonSerializer.Serialize(openAiResponse);
        var httpResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpClient
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.TryMapUnknownAsync(record, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var savedMappings = _dbContext.CarrierMappings.ToList();
        var policyMapping = savedMappings.FirstOrDefault(x => x.TargetField == "PolicyNumber");
        policyMapping?.IsRequired.Should().BeTrue();
        var externalIdMapping = savedMappings.FirstOrDefault(x => x.TargetField == "ExternalId");
        externalIdMapping?.IsRequired.Should().BeTrue();
        var commissionMapping = savedMappings.FirstOrDefault(x => x.TargetField == "NetCommission");
        commissionMapping?.IsRequired.Should().BeFalse();
    }

    [Fact]
    public async Task TryMapUnknownAsync_WithNullMappingFields_FiltersOutEmptyMappings()
    {
        // Arrange
        var record = new RawCarrierRecord(
            "TEST-CARRIER",
            new Dictionary<string, string?>
            {
                { "policy", "POL-001" },
                { "commission", "500.00" }
            }
        );

        var openAiResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = @"[
                            { ""sourceField"": ""policy"", ""targetField"": ""PolicyNumber"" },
                            { ""sourceField"": """", ""targetField"": ""NetCommission"" },
                            { ""sourceField"": ""commission"", ""targetField"": """" }
                        ]"
                    }
                }
            }
        };

        var responseContent = System.Text.Json.JsonSerializer.Serialize(openAiResponse);
        var httpResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpClient
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.TryMapUnknownAsync(record, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var savedMappings = _dbContext.CarrierMappings.ToList();
        savedMappings.Should().HaveCount(1);
        savedMappings.First().SourceField.Should().Be("policy");
        savedMappings.First().TargetField.Should().Be("PolicyNumber");
    }
}
