using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Uruk.Client.FunctionalTests
{
    public class AuditTrailClientFunctionalTests
    {
        private ISecurityEventTokenClient CreateClient(IHost host)
        {
            return host.Services.GetRequiredService<ISecurityEventTokenClient>();
        }

        private IHost CreateHost(HttpResponseMessage response)
        {
            var host = CreateHostBuilder(response).Build();
            host.Start();
            return host;
        }

        [Fact]
        public async Task Post_Success()
        {
            var response = new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.Accepted
            };
            var host = CreateHost(response);
            var client = CreateClient(host);
            var descriptor = CreateDescriptor();
            var result = await client.SendTokenAsync(descriptor);

            Assert.Equal(EventTransmissionStatus.Success, result.Status);
        }

        [Fact]
        public async Task Post_BadRequest_ValidJson()
        {
            var response = new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("{\"err\":\"invalid_request\",\"description\":\"Invalid request\"}")
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var host = CreateHost(response);
            var client = CreateClient(host);
            var descriptor = CreateDescriptor();
            var result = await client.SendTokenAsync(descriptor);

            Assert.Equal(EventTransmissionStatus.Error, result.Status);
            Assert.Equal("invalid_request", result.Error);
            Assert.Equal("Invalid request", result.ErrorDescription);
        }

        [Fact]
        public async Task Post_BadRequest_InvalidJson()
        {
            var response = new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("{}")
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var host = CreateHost(response);
            var client = CreateClient(host);
            var descriptor = CreateDescriptor();
            var result = await client.SendTokenAsync(descriptor);

            Assert.Equal(EventTransmissionStatus.Error, result.Status);
            Assert.Equal("parsing_error", result.Error);
            Assert.NotNull(result.ErrorDescription);
        }

        private static SecurityEventTokenDescriptor CreateDescriptor()
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
            return descriptor;
        }

        private static IHostBuilder CreateHostBuilder(HttpResponseMessage response)
        {
            var handler = new TestHttpMessageHandler(response);
            return Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services
                        .AddSecurityEventTokenClient(o =>
                        {
                            o.EventEndpoint = "https://example.com/events/";
                            o.EncryptionKey = new byte[32];
                        })
                        .ConfigurePrimaryHttpMessageHandler(() => handler)
                        .ConfigureHttpClient(builder =>
                        {
                        });
                });
        }

        private class TestHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _response;

            public TestHttpMessageHandler(HttpResponseMessage response)
            {
                _response = response;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_response);
            }
        }
    }
}
