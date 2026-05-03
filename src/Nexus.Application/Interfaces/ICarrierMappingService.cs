using Nexus.Application.DTOs;
using Nexus.Domain.Entities;

namespace Nexus.Application.Interfaces;

public interface ICarrierMappingService
{
    Task<InsuranceTransaction> MapAsync(Guid batchId, RawCarrierRecord record, CancellationToken cancellationToken);
}
