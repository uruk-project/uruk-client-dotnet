using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.Extensions.Hosting;

namespace Uruk.Client.HostedSample
{
    public class Worker : BackgroundService
    {
        private readonly IAuditTrailClient _client;

        public Worker(IAuditTrailClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var descriptor = new SecurityEventTokenDescriptor()
                {
                    Type = "secevent+jwt",
                    Algorithm = SignatureAlgorithm.HmacSha256,
                    SigningKey = new SymmetricJwk(new byte[16]),
                    Issuer = "https://client.example.com",
                    IssuedAt = DateTime.UtcNow,
                    JwtId = "4d3559ec67504aaba65d40b0363faad8",
                    Audiences = new List<string> { "https://scim.example.com/Feeds/98d52461fa5bbc879593b7754", "https://scim.example.com/Feeds/5d7604516b1d08641d7676ee7" },
                };

                descriptor.AddEvent("urn:ietf:params:scim:event:create", new JwtObject
                {
                    { "ref", "https://scim.example.com/Users/44f6142df96bd6ab61e7521d9"},
                    { "attributes", new JwtArray(new List<string> { "id", "name", "userName", "password", "emails" }) }
                });

                await _client.SendAuditTrailAsync(descriptor);
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
