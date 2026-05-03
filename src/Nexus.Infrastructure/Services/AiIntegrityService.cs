using Nexus.Application.DTOs;
using Nexus.Application.Interfaces;

namespace Nexus.Infrastructure.Services;

public class AiIntegrityService : IAIIntegrityService
{
    public Task<RawCarrierRecord?> TryMapUnknownAsync(RawCarrierRecord record, CancellationToken cancellationToken)
    {
        return Task.FromResult<RawCarrierRecord?>(null);
    }
}
