using FluentAssertions;
using Nexus.Domain.Entities;
using Xunit;

namespace Nexus.Domain.Tests.Entities;

public class InsuranceTransactionTests
{
    [Fact]
    public void Defaults_AreInitialized()
    {
        var transaction = new InsuranceTransaction();

        transaction.ExternalId.Should().BeEmpty();
        transaction.PolicyNumber.Should().BeEmpty();
        transaction.CarrierCode.Should().BeEmpty();
        transaction.PIIMasked.Should().BeFalse();
        transaction.SanitizationLogs.Should().NotBeNull();
    }
}
