using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;

namespace Uruk.Client
{
    public interface IAuditTrailClient
    {
        public Task<AuditTrailPushResponse> SendAuditTrailAsync(SecEventDescriptor descriptor, CancellationToken cancellationToken = default);
  
        public Task<AuditTrailPushResponse> ResendAuditTrailAsync(AuditTrailItem token, CancellationToken cancellationToken = default);
    }
}