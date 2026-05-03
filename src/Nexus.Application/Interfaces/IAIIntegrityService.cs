using Nexus.Application.DTOs;

namespace Nexus.Application.Interfaces;

public interface IAIIntegrityService
{
    Task<RawCarrierRecord?> TryMapUnknownAsync(RawCarrierRecord record, CancellationToken cancellationToken);
}
