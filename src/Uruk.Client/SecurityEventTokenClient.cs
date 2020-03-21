using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;

namespace Uruk.Client
{
    public class SecurityEventTokenClient : ISecurityEventTokenClient
    {
        private readonly HttpClient _httpClient;
        private readonly ITokenSink _sink;
        private readonly JwtWriter _writer;

        public SecurityEventTokenClient(HttpClient httpClient, ITokenSink sink)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _writer = new JwtWriter();
        }

        public int RetryDelay { get; private set; }

        public async Task<SecurityEventTokenPushResponse> SendTokenAsync(SecurityEventTokenDescriptor token, CancellationToken cancellationToken = default)
        {
            using var bufferWriter = new PooledByteBufferWriter(1024);
            _writer.WriteToken(token, bufferWriter);
            var request = new HttpRequestMessage() { Method = HttpMethod.Post };
#if NETSTANDARD2_0
            request.Content = new ByteArrayContent(bufferWriter.Buffer, 0, bufferWriter.Index);
#else
            request.Content = new ReadOnlyMemoryContent(bufferWriter.WrittenMemory);
#endif
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/secevent+jwt");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
                return await SecurityEventTokenPushResponse.FromHttpResponseAsync(response);
            }
            catch (OperationCanceledException exception)
            {
                return await Retry(request, exception, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                // TODO: Warning & persist event for later 
                return await Retry(request, exception, cancellationToken);
            }
            catch (Exception exception)
            {
                return SecurityEventTokenPushResponse.Failure(exception);
            }
        }

        public Task<SecurityEventTokenPushResponse> SendTokenAsync(ReadOnlySpan<byte> request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        private async Task<SecurityEventTokenPushResponse> Retry(HttpRequestMessage failedRequest, Exception exception, CancellationToken cancellationToken)
        {
            var retryMessage = new SecurityEventTokenPushRequest(failedRequest);
            HttpResponseMessage response;
            try
            {
                await Task.Delay(RetryDelay);
                response = await _httpClient.SendAsync(retryMessage, cancellationToken);
            }
            catch (Exception retryException)
            {
                return SecurityEventTokenPushResponse.Failure(new AggregateException(exception, retryException));
            }

            return await SecurityEventTokenPushResponse.FromHttpResponseAsync(response);
        }
    }
}