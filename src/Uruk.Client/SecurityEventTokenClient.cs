using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.Extensions.Logging;

namespace Uruk.Client
{
    public class SecurityEventTokenClient : ISecurityEventTokenClient
    {
        private readonly HttpClient _httpClient;
        private readonly ITokenSink _sink;
        private readonly ILogger<SecurityEventTokenClient> _logger;
        private readonly JwtWriter _writer;

        public SecurityEventTokenClient(HttpClient httpClient, ITokenSink sink, ILogger<SecurityEventTokenClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _writer = new JwtWriter();
        }

        public string Endpoint { get; set; } = "/";

        public Task<SecurityEventTokenPushResponse> SendTokenAsync(Token token, CancellationToken cancellationToken = default)
        {
            HttpContent content = new ByteArrayContent(token.Value);
            return SendTokenAsync(content, cancellationToken);
        }

        public Task<SecurityEventTokenPushResponse> SendTokenAsync(SecurityEventTokenDescriptor token, CancellationToken cancellationToken = default)
        {
            using var bufferWriter = new PooledByteBufferWriter(1024);
            _writer.WriteToken(token, bufferWriter);
#if NETSTANDARD2_0
            var content = new ByteArrayContent(bufferWriter.Buffer, 0, bufferWriter.Index);
#else
            var content = new ReadOnlyMemoryContent(bufferWriter.WrittenMemory);
#endif
            return SendTokenAsync(content, cancellationToken);
        }

        private async Task<SecurityEventTokenPushResponse> SendTokenAsync(HttpContent content, CancellationToken cancellationToken = default)
        {
            using HttpRequestMessage request = CreateRequest(content);
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (OperationCanceledException exception)
            {
                Token token = new Token(await content.ReadAsByteArrayAsync(), 1);
                return Retry(request, token, exception);
            }
            catch (HttpRequestException exception)
            {
                Token token = new Token(await content.ReadAsByteArrayAsync(), 1);
                return Retry(request, token, exception);
            }
            catch (Exception exception)
            {
                return SecurityEventTokenPushResponse.Failure(exception);
            }

            return await SecurityEventTokenPushResponse.FromHttpResponseAsync(response);
        }

        private SecurityEventTokenPushResponse Retry(HttpRequestMessage request, Token token, Exception exception)
        {
            if (_sink.TryWrite(token))
            {
                _logger.LogWarning(exception, $"An error occurred when trying to send token to {request.RequestUri}. The token will be sent again later.");
                return SecurityEventTokenPushResponse.Warning(exception);
            }
            else
            {
                _logger.LogWarning(exception, $"An error occurred when trying to send token to {request.RequestUri}.");
                return SecurityEventTokenPushResponse.Failure(exception);
            }
        }

        private HttpRequestMessage CreateRequest(HttpContent content)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = content,
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/secevent+jwt");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return request;
        }
    }
}