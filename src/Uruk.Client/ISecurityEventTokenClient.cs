using System;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;

namespace Uruk.Client
{
    public interface ISecurityEventTokenClient
    {
        public Task<SecurityEventTokenPushResponse> SendTokenAsync(SecurityEventTokenDescriptor request, CancellationToken cancellationToken = default);
        public Task<SecurityEventTokenPushResponse> SendTokenAsync(ReadOnlySpan<byte> request, CancellationToken cancellationToken = default);
    }
}