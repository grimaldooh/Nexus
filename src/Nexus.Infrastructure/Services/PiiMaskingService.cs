using Nexus.Domain.Entities;
using Nexus.Domain.Interfaces;

namespace Nexus.Infrastructure.Services;

public class PiiMaskingService : IPiiMaskingService
{
    public InsuranceTransaction Mask(InsuranceTransaction transaction)
    {
        transaction.PolicyNumber = MaskValue(transaction.PolicyNumber);
        if (!string.IsNullOrWhiteSpace(transaction.Notes))
        {
            transaction.Notes = MaskValue(transaction.Notes);
        }

        transaction.PIIMasked = true;
        return transaction;
    }

    private static string MaskValue(string value)
    {
        if (value.Length <= 4)
        {
            return new string('*', value.Length);
        }

        var suffix = value[^4..];
        return new string('*', value.Length - 4) + suffix;
    }
}
