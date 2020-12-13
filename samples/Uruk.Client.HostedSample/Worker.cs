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
                var descriptor = new SecEventDescriptor(SymmetricJwk.FromBase64Url("R9MyWaEoyiMYViVWo8Fk4TUGWiSoaW6U1nOqXri8ZXU"), SignatureAlgorithm.HS256)
                {
                    Payload = new JwtPayload
                    {
                        { "iss", "https://client.example.com" },
                        { "iat", EpochTime.UtcNow },
                        { "jti", Guid.NewGuid().ToString("N") },
                        { "aud", new[] { "https://scim.example.com/Feeds/98d52461fa5bbc879593b7754", "https://scim.example.com/Feeds/5d7604516b1d08641d7676ee7" } },
                        { "events", new JsonObject
                            {
                                { "urn:ietf:params:scim:event:create", new JsonObject
                                    {
                                        { "ref", "https://scim.example.com/Users/44f6142df96bd6ab61e7521d9" },
                                        { "attributes", new object[] { "id", "name", "userName", "password", "emails" } }
                                    }
                                }
                            }
                        }
                    }
                };

                await _client.SendAuditTrailAsync(descriptor);
            }
        }
    }
}
