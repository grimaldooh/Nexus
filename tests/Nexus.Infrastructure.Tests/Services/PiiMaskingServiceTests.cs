using FluentAssertions;
using Nexus.Domain.Entities;
using Nexus.Infrastructure.Services;
using Xunit;

namespace Nexus.Infrastructure.Tests.Services;

public class PiiMaskingServiceTests
{
    [Fact]
    public void Mask_WithSensitiveFields_MasksPolicyAndNotes()
    {
        var service = new PiiMaskingService();
        var transaction = new InsuranceTransaction
        {
            PolicyNumber = "12345678",
            Notes = "abcdef"
        };

        var result = service.Mask(transaction);

        result.PolicyNumber.Should().Be("****5678");
        result.Notes.Should().Be("**cdef");
        result.PIIMasked.Should().BeTrue();
    }
}
