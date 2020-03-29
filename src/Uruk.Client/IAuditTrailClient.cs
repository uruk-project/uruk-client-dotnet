using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;

namespace Uruk.Client
{
    public interface IAuditTrailClient
    {
        public Task<AuditTrailPushResponse> SendAuditTrailAsync(SecurityEventTokenDescriptor descriptor, CancellationToken cancellationToken = default);
  
        public Task<AuditTrailPushResponse> ResendAuditRrailAsync(Token token, CancellationToken cancellationToken = default);
    }
}