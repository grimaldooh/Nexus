using FluentAssertions;
using Nexus.Application.DTOs;
using Nexus.Application.Validation;
using Xunit;

namespace Nexus.Application.Tests.Validation;

public class ResolveAuditRequestValidatorTests
{
    [Fact]
    public void Validate_WithMissingFields_ReturnsInvalid()
    {
        var validator = new ResolveAuditRequestValidator();
        var request = new ResolveAuditRequest();

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithValidRequest_ReturnsValid()
    {
        var validator = new ResolveAuditRequestValidator();
        var request = new ResolveAuditRequest
        {
            TransactionId = Guid.NewGuid(),
            Approve = true,
            Reason = "Approved"
        };

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }
}
