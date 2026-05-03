using Nexus.Domain.Entities;

namespace Nexus.Domain.Interfaces;

public interface IPiiMaskingService
{
    InsuranceTransaction Mask(InsuranceTransaction transaction);
}
