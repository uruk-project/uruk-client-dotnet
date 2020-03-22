using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Uruk.Client.Tests
{
    public class UrukClientTests
    {
        private static SecurityEventTokenClient CreateClient(HttpMessageHandler handler, bool tokenSinkResult = true)
        {
            HttpClient httpClient = new HttpClient(handler);
            httpClient.BaseAddress = new Uri("https://uruk.example.com");
            return new SecurityEventTokenClient(httpClient, new TestTokenSink(tokenSinkResult), new TestLogger());
        }

        private class TestTokenSink : ITokenSink
        {
            private readonly bool _success;

            public TestTokenSink(bool success)
            {
                _success = success;
            }

            public Task Flush(ISecurityEventTokenClient client, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public bool TryWrite(Token token)
            {
                return _success;
            }
        }

        [Fact]
        public async Task SendAsync_Accepted_Success()
        {
            var httpClient = CreateClient(new TestHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Accepted)));
            var request = CreateDescriptor();
            var response = await httpClient.SendTokenAsync(request);

            Assert.Equal(EventTransmissionStatus.Success, response.Status);
            Assert.Null(response.Error);
            Assert.Null(response.Description);
            Assert.Null(response.Exception);
        }

        [Theory]
        [InlineData("{\"err\":\"test_error\",\"description\":\"Test description\"}")]
        [InlineData("{\"description\":\"Test description\",\"err\":\"test_error\"}")]
        [InlineData("{\"err\":\"test_error\",\"description\":\"Test description\",\"more\":true}")]
        [InlineData("{\"more\":true,\"err\":\"test_error\",\"description\":\"Test description\"}")]
        public async Task SendAsync_NotAcceptedWithError_Error(string jsonError)
        {
            var message = new HttpResponseMessage(HttpStatusCode.BadRequest);
            message.Content = new StringContent(jsonError);
            var httpClient = CreateClient(new TestHttpMessageHandler(message));
            var request = CreateDescriptor();
            var response = await httpClient.SendTokenAsync(request);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.Equal("test_error", response.Error);
            Assert.Equal("Test description", response.Description);
            Assert.Null(response.Exception);
        }

        [Theory]
        [InlineData("{\"err\":\"test_error\"}")]
        public async Task SendAsync_NotAcceptedWithError_ErrorWithoutDescription(string jsonError)
        {
            var message = new HttpResponseMessage(HttpStatusCode.BadRequest);
            message.Content = new StringContent(jsonError);
            var httpClient = CreateClient(new TestHttpMessageHandler(message));
            var request = CreateDescriptor();
            var response = await httpClient.SendTokenAsync(request);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.Equal("test_error", response.Error);
            Assert.Null(response.Description);
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
            var message = new HttpResponseMessage(HttpStatusCode.BadRequest);
            message.Content = new StringContent(jsonError);
            var httpClient = CreateClient(new TestHttpMessageHandler(message));
            var request = CreateDescriptor();
            var response = await httpClient.SendTokenAsync(request);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.Equal("parsing_error", response.Error);
            Assert.Equal(expectedMessage, response.Description);
            Assert.Null(response.Exception);
        }

        [Fact]
        public async Task SendAsync_NotAcceptedWithInvalidJson_Error()
        {
            string jsonError = "{\"err\":";
            var message = new HttpResponseMessage(HttpStatusCode.BadRequest);
            message.Content = new StringContent(jsonError);
            var httpClient = CreateClient(new TestHttpMessageHandler(message));
            var request = CreateDescriptor();
            var response = await httpClient.SendTokenAsync(request);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.Null(response.Error);
            Assert.Null(response.Description);
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
            var response = await httpClient.SendTokenAsync(request);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.Equal("parsing_error", response.Error);
            Assert.Equal("Error occurred during error message parsing: missing property 'err'.", response.Description);
            Assert.Null(response.Exception);
        }
        
        [Fact]
        public async Task SendAsync_Success()
        {
            var httpClient = CreateClient(new TestHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Accepted)));
            var request = CreateDescriptor();
            var response = await httpClient.SendTokenAsync(request);

            Assert.Equal(EventTransmissionStatus.Success, response.Status);
        }

        [Theory]
        [InlineData(typeof(HttpRequestException))]
        [InlineData(typeof(OperationCanceledException))]
        public async Task SendAsync_Retriable_WarningCaptureException(Type exceptionType)
        {
            var response1 = new HttpResponseMessage { Content = new FailingHttpContent(exceptionType) };
            var httpClient = CreateClient(new TestHttpMessageHandler(response1), tokenSinkResult: true);
            var request = CreateDescriptor();
            var response = await httpClient.SendTokenAsync(request);

            Assert.Equal(EventTransmissionStatus.Warning, response.Status);
            Assert.IsType(exceptionType, response.Exception);
        }

        [Theory]
        [InlineData(typeof(OperationCanceledException))]
        [InlineData(typeof(HttpRequestException))]
        public async Task SendAsync_Retriable_ErrorCaptureException(Type exceptionType)
        {
            var httpClient = CreateClient(new FailingHttpMessageHandler((Exception)Activator.CreateInstance(exceptionType)), false);
            var request = CreateDescriptor();
            var response = await httpClient.SendTokenAsync(request);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.Null(response.Error);
            Assert.Null(response.Description);
            Assert.NotNull(response.Exception);
            Assert.IsType(exceptionType, response.Exception);
        }

        [Theory]
        [InlineData(typeof(Exception))]
        public async Task SendAsync_Exception_Fail(Type exceptionType)
        {
            var response1 = new HttpResponseMessage() { Content = new FailingHttpContent(exceptionType) };
            var httpClient = CreateClient(new TestHttpMessageHandler(response1));
            var request = CreateDescriptor();
            var response = await httpClient.SendTokenAsync(request);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.Null(response.Error);
            Assert.Null(response.Description);
            Assert.NotNull(response.Exception);
            Assert.IsType(exceptionType, response.Exception);
        }

        private static SecurityEventTokenDescriptor CreateDescriptor()
        {
            var descriptor = new SecurityEventTokenDescriptor();
            descriptor.Type = "secevent+jwt";
            descriptor.Algorithm = SignatureAlgorithm.HmacSha256;
            descriptor.SigningKey = new SymmetricJwk(new string('a', 128));
            descriptor.Issuer = "https://client.example.com";
            descriptor.IssuedAt = DateTime.UtcNow;
            descriptor.JwtId = "4d3559ec67504aaba65d40b0363faad8";
            descriptor.Audiences = new List<string> { "https://scim.example.com/Feeds/98d52461fa5bbc879593b7754", "https://scim.example.com/Feeds/5d7604516b1d08641d7676ee7" };
            var @event = new JwtObject();
            @event.Add("ref", "https://scim.example.com/Users/44f6142df96bd6ab61e7521d9");
            @event.Add("attributes", new JwtArray(new List<string> { "id", "name", "userName", "password", "emails" }));
            descriptor.AddEvent("urn:ietf:params:scim:event:create", @event);
            return descriptor;
        }

        private class TestHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _expectedResponse;

            public TestHttpMessageHandler(HttpResponseMessage expectedResponse)
            {
                _expectedResponse = expectedResponse;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = Task.FromResult(_expectedResponse);
                return response;
            }
        }

        private class FailingHttpMessageHandler : HttpMessageHandler
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

        private class FailingHttpContent : HttpContent
        {
            private readonly Type _type;

            public FailingHttpContent(Type type)
            {
                _type = type;
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                throw (Exception)Activator.CreateInstance(_type);
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

        private class TestLogger : ILogger<SecurityEventTokenClient>
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

            private class Scope : IDisposable
            {
                public void Dispose() { }
            }
        }
    }
}
