using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.Extensions.Options;

namespace Uruk.Client
{
    public class UrukClient
    {
        private readonly HttpClient? _httpClient;
        private readonly string _deliveryUri;
        private readonly IHttpClientFactory? _httpClientFactory;
        private readonly JwtWriter _writer = new JwtWriter();

        public UrukClient(IOptions<EventTransmissionOptions> options, IHttpClientFactory httpClientFactory)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var optionsValue = options.Value;
            _deliveryUri = optionsValue.DeliveryUri ?? throw new ArgumentNullException(nameof(options.Value.DeliveryUri));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public UrukClient(string deliveryUri)
            : this(deliveryUri, new HttpClient())
        {
        }

        public UrukClient(string deliveryUri, IHttpClientFactory httpClientFactory)
        {
            _deliveryUri = deliveryUri ?? throw new ArgumentNullException(nameof(deliveryUri));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public UrukClient(string deliveryUri, HttpClient httpClient)
        {
            _deliveryUri = deliveryUri ?? throw new ArgumentNullException(nameof(deliveryUri));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

#if NETSTANDARD2_0
        public Task<EventTransmissionResult> SendAsync(string securityEventToken, CancellationToken cancellationToken = default)
        {
            var raw = Encoding.UTF8.GetBytes(securityEventToken);
            return SendAsync(raw, raw.Length, cancellationToken);
        }
#else
        public Task<EventTransmissionResult> SendAsync(string securityEventToken, CancellationToken cancellationToken = default)
        {
            var raw = Encoding.UTF8.GetBytes(securityEventToken);
            return SendAsync(raw.AsMemory(), cancellationToken);
        }
#endif

        public Task<EventTransmissionResult> SendAsync(SecurityEventTokenDescriptor securityEventToken, CancellationToken cancellationToken = default)
        {
            using var bufferWriter = new PooledByteBufferWriter(1024);
            _writer.WriteToken(securityEventToken, bufferWriter);

#if NETSTANDARD2_0
            // netstandard 2.0 does not support Memory<T>
            return SendAsync(bufferWriter.Buffer, bufferWriter.Index, cancellationToken);
#else
            return SendAsync(bufferWriter.WrittenMemory, cancellationToken);
#endif
        }

#if NETSTANDARD2_0
        public async Task<EventTransmissionResult> SendAsync(byte[] token, int count, CancellationToken cancellationToken = default)
        {
            HttpResponseMessage response;
            try
            {
                response = await SendJwt(token, count, cancellationToken);
            }
#else
        public async Task<EventTransmissionResult> SendAsync(ReadOnlyMemory<byte> token, CancellationToken cancellationToken = default)
        {
            HttpResponseMessage response;
            try
            {
                response = await SendJwt(token, cancellationToken);
            }
#endif
            catch (OperationCanceledException exception)
            {
                // TODO: Retry
                // TODO: Warning & persist event for later 
                return EventTransmissionResult.Error(exception);
            }
            catch (HttpRequestException exception)
            {
                return EventTransmissionResult.Error(exception);
            }

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                return EventTransmissionResult.Success();
            }

            var errorMessage = await response.Content.ReadAsByteArrayAsync();
            if (errorMessage == null)
            {
                return EventTransmissionResult.Error();
            }

            return ReadErrorMessage(errorMessage);
        }

        private static EventTransmissionResult ReadErrorMessage(byte[] errorMessage)
        {
            Utf8JsonReader reader = new Utf8JsonReader(errorMessage);
            if (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
            {
                string? err = null;
                string? description = null;
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    switch (propertyName)
                    {
                        case "err":
                            if (reader.Read() && reader.TokenType == JsonTokenType.String)
                            {
                                err = reader.GetString();
                                if (description != null)
                                {
                                    return EventTransmissionResult.Error(err, description);
                                }
                                continue;
                            }

                            return EventTransmissionResult.Error("parsing_error", "Error occurred during error message parsing: property 'err' must be of type 'string'.");
                     
                        case "description":
                            if (reader.Read() && reader.TokenType == JsonTokenType.String)
                            {
                                description = reader.GetString();
                                if (err != null)
                                {
                                    return EventTransmissionResult.Error(err, description);
                                }

                                continue;
                            }

                            return EventTransmissionResult.Error("parsing_error", "Error occurred during error message parsing: property 'description' must be of type 'string'.");
                  
                        default:
                            // Skip the unattended properties
                            reader.Skip();
                            continue;
                    }
                }

                if (err != null)
                {
                    return EventTransmissionResult.Error(err, description);
                }
                else
                {
                    return EventTransmissionResult.Error("parsing_error", "Error occurred during error message parsing: missing property 'err'.");
                }
            }

            return EventTransmissionResult.Error("parsing_error", "Error occurred during error message parsing: invalid JSON." +
                Environment.NewLine + "The error message is:" +
                Environment.NewLine + Encoding.UTF8.GetString(errorMessage));
        }

#if NETSTANDARD2_0
        private Task<HttpResponseMessage> SendJwt(byte[] token, int count, CancellationToken cancellationToken)
        {
            return SendJwt(new ByteArrayContent(token, 0, count), cancellationToken);
        }
#else
        private Task<HttpResponseMessage> SendJwt(ReadOnlyMemory<byte> token, CancellationToken cancellationToken)
        {
            return SendJwt(new ReadOnlyMemoryContent(token), cancellationToken);
        }
#endif

        private Task<HttpResponseMessage> SendJwt(HttpContent content, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _deliveryUri);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/secevent+jwt");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = content;
            var httpClient = _httpClient ?? _httpClientFactory.CreateClient();
            return httpClient.SendAsync(request, cancellationToken);
        }
    }
}