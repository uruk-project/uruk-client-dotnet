﻿using System;
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
    public class AuditTrailClient : IAuditTrailClient
    {
        private readonly HttpClient _httpClient;
        private readonly AuditTrailClientOptions _options;
        private readonly IAuditTrailSink _sink;
        private readonly ILogger<AuditTrailClient> _logger;
        private readonly IHostEnvironment? _env;
        private readonly JwtWriter _writer;
        private readonly IAuditTrailStore _store;

        public AuditTrailClient(HttpClient httpClient, IOptions<AuditTrailClientOptions> options, IAuditTrailSink sink, IAuditTrailStore store, ILogger<AuditTrailClient> logger, IHostEnvironment? env = null)
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
        public AuditTrailClient(string eventEndpoint)
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
            _options = new AuditTrailClientOptions
            {
                EventEndpoint = eventEndpoint
            };
        }

        public bool IsHosted => !(_env is null);

        public async Task<AuditTrailPushResponse> ResendAuditRrailAsync(Token token, CancellationToken cancellationToken = default)
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
                    result = AuditTrailPushResponse.Failure(result);
                    break;
            }

            return result;
        }

        public async Task<AuditTrailPushResponse> SendAuditTrailAsync(SecurityEventTokenDescriptor descriptor, CancellationToken cancellationToken = default)
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
                    var filename = await _store.RecordAuditTrailAsync(data);
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
                            result = AuditTrailPushResponse.Failure(result.HttpStatusCode.Value);
                        }
                        else
                        {
                            result = AuditTrailPushResponse.Failure(result);
                        }
                    }
                }
                else
                {
                    // If the application is not hosted, convert warning to error
                    result = AuditTrailPushResponse.Failure(result);
                }
            }

            return result;
        }

        private async Task<AuditTrailPushResponse> SendTokenAsync(HttpContent content, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = CreateRequest(content);
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (OperationCanceledException exception)
            {
                return AuditTrailPushResponse.Warning(exception);
            }
            catch (HttpRequestException exception)
            {
                return AuditTrailPushResponse.Warning(exception);
            }
            catch (Exception exception)
            {
                return AuditTrailPushResponse.Failure(exception);
            }

            if (response.StatusCode == HttpStatusCode.RequestTimeout || ((int)response.StatusCode >= 500) && ((int)response.StatusCode <= 599))
            {
                return AuditTrailPushResponse.Warning(response.StatusCode);
            }

            return await AuditTrailPushResponse.FromHttpResponseAsync(response);
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

        private class NullSink : IAuditTrailSink
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

        private class ConsoleLogger : ILogger<AuditTrailClient>
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

        private class NullStore : IAuditTrailStore
        {
            public void DeleteRecord(Token token)
            {
            }

            public IEnumerable<Token> GetAllAuditTrailRecords()
            {
                yield break;
            }

            public Task<string> RecordAuditTrailAsync(byte[] token)
            {
                return Task.FromResult(string.Empty);
            }
        }
    }
}