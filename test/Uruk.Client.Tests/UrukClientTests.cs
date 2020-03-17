using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.Extensions.Options;
using Xunit;

namespace Uruk.Client.Tests
{
    public class UrukClientTests
    {
        [Fact]
        public void Ctor_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => new UrukClient((IOptions<EventTransmissionOptions>)null, new TestHttpClientFactory()));
            Assert.Throws<ArgumentNullException>(() => new UrukClient(Options.Create(new EventTransmissionOptions()), new TestHttpClientFactory()));
            Assert.Throws<ArgumentNullException>(() => new UrukClient(null));
            Assert.Throws<ArgumentNullException>(() => new UrukClient((string)null, new TestHttpClientFactory()));
            Assert.Throws<ArgumentNullException>(() => new UrukClient("https://uruk.example.com", (IHttpClientFactory)null));
            Assert.Throws<ArgumentNullException>(() => new UrukClient(null, new HttpClient()));
            Assert.Throws<ArgumentNullException>(() => new UrukClient("https://uruk.example.com", (HttpClient)null));
        }

        [Fact]
        public async Task SendAsync_Accepted_Success()
        {
            var httpClient = new HttpClient(new TestHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Accepted)));
            var client = new UrukClient("https://uruk.example.com", httpClient);
            SecurityEventTokenDescriptor descriptor = CreateDescriptor();

            var response = await client.SendAsync(descriptor);

            Assert.Equal(EventTransmissionStatus.Success, response.Status);
            Assert.Null(response.ErrorMessage);
            Assert.Null(response.Exception);
        }

        [Fact]
        public async Task SendAsync_NotAccepted_Error()
        {
            var message = new HttpResponseMessage(HttpStatusCode.BadRequest);
            message.Content = new StringContent("{\"err\":\"test_error\",\"description\":\"Test description\"}");
            var httpClient = new HttpClient(new TestHttpMessageHandler(message));
            var client = new UrukClient("https://uruk.example.com", httpClient);
            SecurityEventTokenDescriptor descriptor = CreateDescriptor();

            var response = await client.SendAsync(descriptor);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.NotNull(response.ErrorMessage);
            Assert.Equal("test_error", response.ErrorMessage.Error);
            Assert.Equal("Test description", response.ErrorMessage.Description);
            Assert.Null(response.Exception);
        }

        [Fact]
        public async Task SendAsync_OperationCanceledException_CaptureException()
        {
            var httpClient = new HttpClient(new FailingHttpMessageHandler(new OperationCanceledException()));
            var client = new UrukClient("https://uruk.example.com", httpClient);
            SecurityEventTokenDescriptor descriptor = CreateDescriptor();

            var response = await client.SendAsync(descriptor);

            Assert.Equal(EventTransmissionStatus.Error, response.Status);
            Assert.Null(response.ErrorMessage);
            Assert.NotNull(response.Exception);
            Assert.IsType<OperationCanceledException>(response.Exception);
        }

        [Fact]
        public async Task SendAsync_UnmanagedException_DoNotCaptureException()
        {
            var httpClient = new HttpClient(new FailingHttpMessageHandler(new InvalidOperationException()));
            var client = new UrukClient("https://uruk.example.com", httpClient);
            SecurityEventTokenDescriptor descriptor = CreateDescriptor();

            await Assert.ThrowsAnyAsync<Exception>(() => client.SendAsync(descriptor));
        }

        [Fact]
        public async Task SendAsync()
        {
            var httpClient = new HttpClient(new TestHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Accepted)));
            var client = new UrukClient("https://uruk.example.com", httpClient);
            SecurityEventTokenDescriptor descriptor = CreateDescriptor();

            var response = await client.SendAsync(descriptor);

            Assert.Equal(EventTransmissionStatus.Success, response.Status);
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

        private class TestHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClient _httpClient;

            public TestHttpClientFactory()
                : this(new HttpClient())
            {
            }

            public TestHttpClientFactory(HttpClient httpClient)
            {
                _httpClient = new HttpClient();
            }

            public HttpClient CreateClient(string name)
            {
                return _httpClient;
            }
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
                return Task.FromResult(_expectedResponse);
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
        }
    }
}
