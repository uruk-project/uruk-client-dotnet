using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;

namespace Uruk.Client
{
    public interface ISecurityEventTokenClient
    {
        public Task<SecurityEventTokenPushResponse> SendTokenAsync(SecurityEventTokenDescriptor descriptor, CancellationToken cancellationToken = default);
  
        public Task<SecurityEventTokenPushResponse> ResendTokenAsync(Token token, CancellationToken cancellationToken = default);
    }
}