using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IdentityModel.Client;
using JsonWebToken;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Uruk.Client.Tests
{
    public class AuditTrailClientTests
    {
        [Fact]
        public void Ctor()
        {
            var httpClient = new HttpClient();
            var options = Options.Create(new AuditTrailClientOptions { DeliveryEndpoint = "https://example.com" });
            var tokenClientOptions = options.Value.TokenClientOptions;
            var sink = new TestTokenSink(true);
            var store = new TestTokenStore();
            var tokenAcquirer = new DefaultAccessTokenAcquirer(new TestLogger<DefaultAccessTokenAcquirer>(), new TokenClient(httpClient, options.Value.TokenClientOptions), options);
            var logger = new TestLogger<AuditTrailClient>();
            var env = new TestHostEnvironment();

            var client = new AuditTrailClient("https://example.com", "api", tokenClientOptions);
            client = new AuditTrailClient(httpClient, options, sink, store, logger, tokenAcquirer, env);
            client = new AuditTrailClient(httpClient, options, sink, store, logger, tokenAcquirer);

            Assert.Throws<ArgumentNullException>("eventEndpoint", () => new AuditTrailClient(null!, "api", tokenClientOptions));
            Assert.Throws<ArgumentNullException>("scope", () => new AuditTrailClient("https://example.com", null!, tokenClientOptions));
            Assert.Throws<ArgumentNullException>("tokenClientOptions", () => new AuditTrailClient("https://example.com", "api", null!));
            Assert.Throws<ArgumentNullException>("httpClient", () => new AuditTrailClient(null!, options, sink, store, logger, tokenAcquirer, env));
            Assert.Throws<ArgumentNullException>("options", () => new AuditTrailClient(httpClient, null!, sink, store, logger, tokenAcquirer, env));
            Assert.Throws<ArgumentNullException>("sink", () => new AuditTrailClient(httpClient, options, null!, store, logger, tokenAcquirer, env));
            Assert.Throws<ArgumentNullException>("store", () => new AuditTrailClient(httpClient, options, sink, null!, logger, tokenAcquirer, env));
            Assert.Throws<ArgumentNullException>("logger", () => new AuditTrailClient(httpClient, options, sink, store, null!, tokenAcquirer, env));
            Assert.Throws<ArgumentNullException>("tokenAcquirer", () => new AuditTrailClient(httpClient, options, sink, store, logger, null!, env));
            Assert.Throws<ArgumentException>("options", () => new AuditTrailClient(httpClient, Options.Create(new AuditTrailClientOptions()), sink, store, logger, tokenAcquirer, env));
        }

        [Fact]
        public async Task SendAsync_Accepted_Success()
        {
            var httpClient = CreateClient(new TestHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Accepted)));
            var request = CreateDescriptor();
            var response = await httpClient.SendAuditTrailAsync(request);

            Assert.Equal(EventTransmissionStatus.Success, response.Status);
            Assert.Null(response.Error);
            Assert.Null(response.ErrorDescription);
            Assert.Null(response.Exception);
        }

        [Theory]
        [InlineData("{\"err\":\"test_error\",\"description\":\"Test description\"}")]
        [InlineData("{\"description\":\"Test description\",\"err\":\"test_error\"}")]
        [InlineData("{\"err\":\"test_error\",\"description\":\"Test description\",\"more\":true}")]
        [InlineData("{\"more\":true,\"err\":\"test_error\",\"description\":\"Test description\"}")]
        public async Task SendAsync_NotAcceptedWithError_Error(string jsonError)
        {
            var message = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(jsonError)
            };
            var httpClient = CreateClient(new TestHttpMessageHandler(message));
            var request = CreateDescriptor();
            var response = await httpClient.SendAuditTrailAsync(request);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.Equal("test_error", response.Error);
            Assert.Equal("Test description", response.ErrorDescription);
            Assert.Null(response.Exception);
        }

        [Fact]
        public async Task SendAsync_NotAcceptedWithBadContentType_Error()
        {
            var message = new HttpResponseMessage(HttpStatusCode.BadRequest);
            var httpClient = CreateClient(new TestHttpMessageHandler(message, setDefaultContentType: false));
            var request = CreateDescriptor();
            var response = await httpClient.SendAuditTrailAsync(request);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.Null(response.Error);
            Assert.Null(response.ErrorDescription);
            Assert.Null(response.Exception);
        }

        [Theory]
        [InlineData("{\"err\":\"test_error\"}")]
        public async Task SendAsync_NotAcceptedWithError_ErrorWithoutDescription(string jsonError)
        {
            var message = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(jsonError)
            };
            var httpClient = CreateClient(new TestHttpMessageHandler(message));
            var request = CreateDescriptor();
            var response = await httpClient.SendAuditTrailAsync(request);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.Equal("test_error", response.Error);
            Assert.Null(response.ErrorDescription);
            Assert.Null(response.Exception);
        }

        [Theory]
        [InlineData("{\"err\":123,\"description\":\"Test description\"}", "Error occurred during error message parsing: property 'err' must be of type 'string'.")]
        [InlineData("{\"err\":\"test_error\",\"description\":123}", "Error occurred during error message parsing: property 'description' must be of type 'string'.")]
        [InlineData("{\"description\":\"Test description\"}", "Error occurred during error message parsing: missing property 'err'.")]
        [InlineData("{}", "Error occurred during error message parsing: missing property 'err'.")]
        [InlineData("[\"hello\",\"world\"]", "Error occurred during error message parsing: invalid JSON.\nThe error message is:\n[\"hello\",\"world\"]")]
        public async Task SendAsync_NotAcceptedWithUnparsableError_Error(string jsonError, string expectedMessage)
        {
            expectedMessage = expectedMessage.Replace("\n", Environment.NewLine);
            var message = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(jsonError)
            };
            var httpClient = CreateClient(new TestHttpMessageHandler(message));
            var request = CreateDescriptor();
            var response = await httpClient.SendAuditTrailAsync(request);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.Equal("parsing_error", response.Error);
            Assert.Equal(expectedMessage, response.ErrorDescription);
            Assert.Null(response.Exception);
        }

        [Fact]
        public async Task SendAsync_NotAcceptedWithInvalidJson_Error()
        {
            string jsonError = "{\"err\":";
            var message = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(jsonError)
            };
            var httpClient = CreateClient(new TestHttpMessageHandler(message));
            var request = CreateDescriptor();
            var response = await httpClient.SendAuditTrailAsync(request);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.Null(response.Error);
            Assert.Null(response.ErrorDescription);
            Assert.NotNull(response.Exception);
            Assert.IsAssignableFrom<JsonException>(response.Exception);
        }

        [Fact]
        public async Task SendAsync_NotAcceptedWithoutError_NoError()
        {
            var message = new HttpResponseMessage(HttpStatusCode.BadRequest);
            var jsonError = "{\"errrrrr\":\"test_error\",\"description\":\"Test description\"}";
            message.Content = new StringContent(jsonError);
            var httpClient = CreateClient(new TestHttpMessageHandler(message));
            var request = CreateDescriptor();
            var response = await httpClient.SendAuditTrailAsync(request);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.Equal("parsing_error", response.Error);
            Assert.Equal("Error occurred during error message parsing: missing property 'err'.", response.ErrorDescription);
            Assert.Null(response.Exception);
        }

        [Fact]
        public async Task SendAsync_Success()
        {
            var httpClient = CreateClient(new TestHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Accepted)));
            var request = CreateDescriptor();
            var response = await httpClient.SendAuditTrailAsync(request);

            Assert.Equal(EventTransmissionStatus.Success, response.Status);
        }

        [Theory]
        [InlineData(typeof(HttpRequestException))]
        [InlineData(typeof(OperationCanceledException))]
        public async Task SendAsync_Retriable_Hosted_WarningCaptureException(Type exceptionType)
        {
            var store = new TestTokenStore();
            var response1 = new HttpResponseMessage { Content = new FailingHttpContent(exceptionType) };
            var httpClient = CreateClient(new TestHttpMessageHandler(response1), tokenSinkResult: true, store: store, env: new TestHostEnvironment());
            var request = CreateDescriptor();
            var response = await httpClient.SendAuditTrailAsync(request);

            Assert.Equal(EventTransmissionStatus.ShouldRetry, response.Status);
            Assert.IsType(exceptionType, response.Exception);
            Assert.Equal(1, store.RecordedCount);
        }

        [Theory]
        [InlineData(typeof(HttpRequestException))]
        [InlineData(typeof(OperationCanceledException))]
        public async Task SendAsync_Retriable_NotHosted_WarningCaptureException(Type exceptionType)
        {
            var store = new TestTokenStore();
            var response1 = new HttpResponseMessage { Content = new FailingHttpContent(exceptionType) };
            var httpClient = CreateClient(new TestHttpMessageHandler(response1), tokenSinkResult: true, store: store);
            var request = CreateDescriptor();
            var response = await httpClient.SendAuditTrailAsync(request);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.IsType(exceptionType, response.Exception);
            Assert.Equal(0, store.RecordedCount);
        }

        [Theory]
        [InlineData(typeof(OperationCanceledException))]
        [InlineData(typeof(HttpRequestException))]
        public async Task SendAsync_Retriable_Hosted_ErrorCaptureException(Type exceptionType)
        {
            var store = new TestTokenStore();
            var httpClient = CreateClient(new FailingHttpMessageHandler((Exception)Activator.CreateInstance(exceptionType)!), tokenSinkResult: false, store: store, env: new TestHostEnvironment());
            var request = CreateDescriptor();
            var response = await httpClient.SendAuditTrailAsync(request);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.Null(response.Error);
            Assert.Null(response.ErrorDescription);
            Assert.NotNull(response.Exception);
            Assert.IsType(exceptionType, response.Exception);

            Assert.Equal(1, store.RecordedCount);
        }

        [Theory]
        [InlineData(typeof(OperationCanceledException))]
        [InlineData(typeof(HttpRequestException))]
        public async Task SendAsync_Retriable_NotHosted_ErrorCaptureException(Type exceptionType)
        {
            var store = new TestTokenStore();
            var httpClient = CreateClient(new FailingHttpMessageHandler((Exception)Activator.CreateInstance(exceptionType)!), tokenSinkResult: false, store: store, env: null);
            var request = CreateDescriptor();
            var response = await httpClient.SendAuditTrailAsync(request);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.Null(response.Error);
            Assert.Null(response.ErrorDescription);
            Assert.NotNull(response.Exception);
            Assert.IsType(exceptionType, response.Exception);

            Assert.Equal(0, store.RecordedCount);
        }

        [Theory]
        [InlineData(typeof(Exception))]
        public async Task SendAsync_Exception_Fail(Type exceptionType)
        {
            var store = new TestTokenStore();
            var response1 = new HttpResponseMessage() { Content = new FailingHttpContent(exceptionType) };
            var httpClient = CreateClient(new TestHttpMessageHandler(response1), store: store);
            var request = CreateDescriptor();
            var response = await httpClient.SendAuditTrailAsync(request);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.Null(response.Error);
            Assert.Null(response.ErrorDescription);
            Assert.NotNull(response.Exception);
            Assert.IsType(exceptionType, response.Exception);

            Assert.Equal(0, store.RecordedCount);
        }

        private static AuditTrailClient CreateClient(HttpMessageHandler handler, HttpMessageHandler? tokenHandler = null, bool tokenSinkResult = true, IAuditTrailStore? store = null, IHostEnvironment? env = null)
        {
            var options = new AuditTrailClientOptions { DeliveryEndpoint = "https://uruk.example.com/events" };
            HttpClient httpClient = new HttpClient(handler);
            return new AuditTrailClient(
                httpClient,
                Options.Create(options),
                new TestTokenSink(tokenSinkResult),
                store ?? new TestTokenStore(),
                new TestLogger<AuditTrailClient>(),
                new NullAccessTokenAcquisitor(),
                env);
        }

        private sealed class NullAccessTokenAcquisitor : IAccessTokenAcquirer
        {
            public Task<string?> AcquireAccessTokenAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<string?>(string.Empty);
            }
        }

        private class TestTokenSink : IAuditTrailSink
        {
            private readonly bool _success;

            public TestTokenSink(bool success)
            {
                _success = success;
            }

            public Task Stop(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public bool TryRead(out Token token)
            {
                token = default;
                return _success;
            }

            public bool TryWrite(Token token)
            {
                return _success;
            }

            public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
            {
                return new ValueTask<bool>(_success);
            }
        }

        private static SecurityEventTokenDescriptor CreateDescriptor()
        {
            var descriptor = new SecurityEventTokenDescriptor
            {
                Type = "secevent+jwt",
                Algorithm = SignatureAlgorithm.HmacSha256,
                SigningKey = new SymmetricJwk(new byte[128]),
                Issuer = "https://client.example.com",
                IssuedAt = DateTime.UtcNow,
                JwtId = "4d3559ec67504aaba65d40b0363faad8",
                Audiences = new List<string> { "https://scim.example.com/Feeds/98d52461fa5bbc879593b7754", "https://scim.example.com/Feeds/5d7604516b1d08641d7676ee7" }
            };
            var @event = new JwtObject
            {
                { "ref", "https://scim.example.com/Users/44f6142df96bd6ab61e7521d9" },
                { "attributes", new JwtArray(new List<string> { "id", "name", "userName", "password", "emails" }) }
            };
            descriptor.AddEvent("urn:ietf:params:scim:event:create", @event);
            return descriptor;
        }

        private class TestTokenStore : IAuditTrailStore
        {
            public int RecordedCount { get; set; }

            public void DeleteRecord(Token token)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<Token> GetAllAuditTrailRecords()
            {
                throw new NotImplementedException();
            }

            public Task<string> RecordAuditTrailAsync(byte[] token)
            {
                RecordedCount++;
                return Task.FromResult<string>(string.Empty);
            }
        }
        private class TestHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public string ApplicationName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public string ContentRootPath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public IFileProvider ContentRootFileProvider { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        }
    }

    internal class Scope : IDisposable
    {
        public void Dispose() { }
    }

    internal class TestLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state)
        {
            return new Scope();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
        }
    }

    internal class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _expectedResponse;

        public TestHttpMessageHandler(HttpResponseMessage expectedResponse, bool setDefaultContentType = true)
        {
            _expectedResponse = expectedResponse;
            if (_expectedResponse.Content is null)
            {
                _expectedResponse.Content = new StringContent("");
            }

            if (setDefaultContentType)
            {
                _expectedResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = Task.FromResult(_expectedResponse);
            return response;
        }
    }

    internal class FailingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public FailingHttpMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw _exception;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }

    internal class FailingHttpContent : HttpContent
    {
        private readonly Type _type;

        public FailingHttpContent(Type type)
        {
            _type = type;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            throw (Exception)Activator.CreateInstance(_type)!;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
        }
    }

}
