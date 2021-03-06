using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Uruk.Client.FunctionalTests
{
    public class AuditTrailClientFunctionalTests : IDisposable
    {
        private readonly string _directory;

        public AuditTrailClientFunctionalTests()
        {
            const string tokensFallbackDir = "SET_TOKENS_FALLBACK_DIR";
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                        ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                        ?? Environment.GetEnvironmentVariable(tokensFallbackDir);

            _directory = Path.Combine(root!, Constants.DefaultStorageDirectory);
            DeleteFiles();
        }

        private IAuditTrailClient CreateClient(IHost host)
        {
            return host.Services.GetRequiredService<IAuditTrailClient>();
        }

        private IHost CreateHost(HttpResponseMessage response)
        {
            return BuildHost(CreateHostBuilder(response));
        }

        private IHost CreateHost(Exception exception)
        {
            return BuildHost(CreateHostBuilder(exception: exception));
        }

        private IHost BuildHost(IHostBuilder builder)
        {
            var host = builder.Build();
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
            var result = await client.SendAuditTrailAsync(descriptor);

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
            var result = await client.SendAuditTrailAsync(descriptor);

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
            var result = await client.SendAuditTrailAsync(descriptor);

            Assert.Equal(EventTransmissionStatus.Error, result.Status);
            Assert.Equal("parsing_error", result.Error);
            Assert.NotNull(result.ErrorDescription);
        }

        [Fact]
        public async Task Post_SendFailed()
        {
            var host = CreateHost(new HttpRequestException());
            var client = CreateClient(host);
            var descriptor = CreateDescriptor();
            var result = await client.SendAuditTrailAsync(descriptor);

            Assert.Equal(EventTransmissionStatus.ShouldRetry, result.Status);
            Assert.Null(result.Error);
            Assert.Null(result.ErrorDescription);

            Assert.Single(Directory.EnumerateFiles(_directory, "*.token", SearchOption.TopDirectoryOnly));
        }

        public void Dispose()
        {
            DeleteFiles();
        }

        private void DeleteFiles()
        {
            if (Directory.Exists(_directory))
            {
                foreach (var filename in Directory.EnumerateFiles(_directory, "*.token", SearchOption.TopDirectoryOnly))
                {
                    File.Delete(filename);
                }
            }
        }

        private static SecEventDescriptor CreateDescriptor()
        {
            var descriptor = new SecEventDescriptor(SymmetricJwk.FromByteArray(new byte[16]), SignatureAlgorithm.HS256)
            {
                Payload = new JwtPayload
                {
                    {"iss", "https://client.example.com" },
                    {"iat", EpochTime.UtcNow },
                    {"jti", "4d3559ec67504aaba65d40b0363faad8" },
                    {"aud", new[] { "https://scim.example.com/Feeds/98d52461fa5bbc879593b7754", "https://scim.example.com/Feeds/5d7604516b1d08641d7676ee7" } },
                    { "events", new JsonObject
                        {
                            { "urn:ietf:params:scim:event:create", new JsonObject
                                {
                                    { "ref", "https://scim.example.com/Users/44f6142df96bd6ab61e7521d9"},
                                    { "attributes", new object[] { "id", "name", "userName", "password", "emails" } }
                                }
                            }
                        }
                    }
                }
            };

            return descriptor;
        }

        private static IHostBuilder CreateHostBuilder(HttpResponseMessage? response = null, Exception? exception = null)
        {
            var handler = response != null ? new TestHttpMessageHandler(response) : new TestHttpMessageHandler(exception);
            return CreateHostBuilder(handler);
        }

        private static IHostBuilder CreateHostBuilder(TestHttpMessageHandler handler)
        {
            return Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services
                        .AddAuditTrailClient(o =>
                        {
                            o.DeliveryEndpoint = "https://example.com/events/";
                            o.TemporaryStorageEncryptionKey = SymmetricJwk.FromByteArray(new byte[32]);
                        })
                        .ConfigurePrimaryHttpMessageHandler(() => handler)
                        .ConfigureHttpClient(builder =>
                        {
                        });
                    services.Replace(new ServiceDescriptor(typeof(IAccessTokenAcquirer), typeof(NullTokenAcquirer), ServiceLifetime.Singleton));
                });
        }

        private class NullTokenAcquirer : IAccessTokenAcquirer
        {
            public Task<string?> AcquireAccessTokenAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult((string?)null);
            }
        }

        private class TestHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage? _response;
            private readonly Exception? _exception;

            public TestHttpMessageHandler(HttpResponseMessage response)
            {
                _response = response;
            }

            public TestHttpMessageHandler(Exception? exception)
            {
                _exception = exception;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (_response != null)
                {
                    return Task.FromResult(_response);
                }
                else if (_exception != null)
                {
                    return Task.FromException<HttpResponseMessage>(_exception);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }
}
