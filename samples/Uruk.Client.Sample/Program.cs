using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityModel.Client;
using JsonWebToken;

namespace Uruk.Client
{
    class Program
    {
        static async Task Main()
        {
            var client = new AuditTrailClient("https://example.com", "api", new TokenClientOptions {  });

            var descriptor = new SecEventDescriptor(SymmetricJwk.FromBase64Url("R9MyWaEoyiMYViVWo8Fk4TUGWiSoaW6U1nOqXri8ZXU"), SignatureAlgorithm.HS256)
            {
                Payload = new JwtPayload
                    {
                        { "iss", "https://client.example.com" },
                        { "iat", EpochTime.UtcNow },
                        { "jti", "4d3559ec67504aaba65d40b0363faad8" },
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

            var result = await client.SendAuditTrailAsync(descriptor);
            Console.WriteLine(result.Status);
        }
    }
}
