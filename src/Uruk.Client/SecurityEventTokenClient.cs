using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Uruk.Client
{
    public class SecurityEventTokenClient : ISecurityEventTokenClient
    {
        private readonly HttpClient _httpClient;
        private readonly SecurityEventTokenClientOptions _options;
        private readonly ITokenSink _sink;
        private readonly ILogger<SecurityEventTokenClient> _logger;
        private readonly IHostEnvironment? _env;
        private readonly JwtWriter _writer;
        private readonly ITokenStore _store;

        public SecurityEventTokenClient(HttpClient httpClient, IOptions<SecurityEventTokenClientOptions> options, ITokenSink sink, ITokenStore store, ILogger<SecurityEventTokenClient> logger, IHostEnvironment? env = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _env = env;
            _options = options.Value;
            _writer = new JwtWriter();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventEndpoint"></param>
        public SecurityEventTokenClient(string eventEndpoint)
        {
            if (eventEndpoint is null)
            {
                throw new ArgumentNullException(nameof(eventEndpoint));
            }

            _httpClient = new HttpClient();
            _sink = new NullSink();
            _logger = new ConsoleLogger();
            _writer = new JwtWriter();
            _store = new NullStore();
            _options = new SecurityEventTokenClientOptions
            {
                EventEndpoint = eventEndpoint
            };
        }

        public bool IsHosted => !(_env is null);

        public async Task<SecurityEventTokenPushResponse> ResendTokenAsync(Token token, CancellationToken cancellationToken = default)
        {
            HttpContent content = new ByteArrayContent(token.Value);
            var result = await SendTokenAsync(content, cancellationToken);
            switch (result.Status)
            {
                // After a failure, sending is a success. We can delete the token record.
                case EventTransmissionStatus.Success:
                    _store.DeleteRecord(token);
                    break;

                case EventTransmissionStatus.Warning when IsHosted:
                    token.RetryCount++;
                    if (_sink.TryWrite(token))
                    {
                        _logger.SendingTokenFailed(_options.EventEndpoint, result.HttpStatusCode, result.Exception);
                    }
                    else
                    {
                        _logger.SendingTokenFailedNoRetry(_options.EventEndpoint, result.HttpStatusCode, result.Exception);
                    }

                    break;

                case EventTransmissionStatus.Warning when IsHosted:
                    // If the application is not hosted, convert warning to error
                    result = SecurityEventTokenPushResponse.Failure(result);
                    break;
            }

            return result;
        }

        public async Task<SecurityEventTokenPushResponse> SendTokenAsync(SecurityEventTokenDescriptor descriptor, CancellationToken cancellationToken = default)
        {
            using var bufferWriter = new PooledByteBufferWriter(1024);
            _writer.WriteToken(descriptor, bufferWriter);
#if NETSTANDARD2_0
            var content = new ByteArrayContent(bufferWriter.Buffer, 0, bufferWriter.WrittenCount);
#else
            var content = new ReadOnlyMemoryContent(bufferWriter.WrittenMemory);
#endif
            var result = await SendTokenAsync(content, cancellationToken);

            if (result.Status == EventTransmissionStatus.Warning)
            {
                if (IsHosted)
                {
                    var data = bufferWriter.WrittenSpan.ToArray();
                    var filename = await _store.RecordTokenAsync(data);
                    var token = new Token(data, filename, 0);
                    if (_sink.TryWrite(token))
                    {
                        _logger.SendingTokenFailed(_options.EventEndpoint, result.HttpStatusCode, result.Exception);
                    }
                    else
                    {
                        _logger.SendingTokenFailedNoRetry(_options.EventEndpoint, result.HttpStatusCode, result.Exception);
                        if (result.HttpStatusCode.HasValue)
                        {
                            result = SecurityEventTokenPushResponse.Failure(result.HttpStatusCode.Value);
                        }
                        else
                        {
                            result = SecurityEventTokenPushResponse.Failure(result);
                        }
                    }
                }
                else
                {
                    // If the application is not hosted, convert warning to error
                    result = SecurityEventTokenPushResponse.Failure(result);
                }
            }

            return result;
        }

        private async Task<SecurityEventTokenPushResponse> SendTokenAsync(HttpContent content, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = CreateRequest(content);
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (OperationCanceledException exception)
            {
                return SecurityEventTokenPushResponse.Warning(exception);
            }
            catch (HttpRequestException exception)
            {
                return SecurityEventTokenPushResponse.Warning(exception);
            }
            catch (Exception exception)
            {
                return SecurityEventTokenPushResponse.Failure(exception);
            }

            if (response.StatusCode == HttpStatusCode.RequestTimeout || ((int)response.StatusCode >= 500) && ((int)response.StatusCode <= 599))
            {
                return SecurityEventTokenPushResponse.Warning(response.StatusCode);
            }

            return await SecurityEventTokenPushResponse.FromHttpResponseAsync(response);
        }

        private HttpRequestMessage CreateRequest(HttpContent content)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _options.EventEndpoint)
            {
                Content = content,
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/secevent+jwt");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return request;
        }

        private class NullSink : ITokenSink
        {
            public Task Stop(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public bool TryRead(out Token token)
            {
                token = default;
                return true;
            }

            public bool TryWrite(Token token)
            {
                return false;
            }

            public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
            {
                return new ValueTask<bool>(Task.FromResult(true));
            }
        }

        private class ConsoleLogger : ILogger<SecurityEventTokenClient>
        {
            public IDisposable BeginScope<TState>(TState state)
            {
                throw new NotImplementedException();
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                Console.WriteLine(formatter(state, exception));
            }
        }

        private class NullStore : ITokenStore
        {
            public void DeleteRecord(Token token)
            {
            }

            public IEnumerable<Token> GetAllTokenRecords()
            {
                yield break;
            }

            public Task<string> RecordTokenAsync(byte[] token)
            {
                return Task.FromResult(string.Empty);
            }
        }
    }
}