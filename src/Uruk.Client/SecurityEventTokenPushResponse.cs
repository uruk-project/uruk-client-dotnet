using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Uruk.Client
{
    public class SecurityEventTokenPushResponse
    {
        public SecurityEventTokenPushResponse(EventTransmissionStatus status, HttpStatusCode httpStatusCode, byte[] raw)
        {
            HttpStatusCode = httpStatusCode;
            Status = status;
            Raw = raw ?? throw new ArgumentNullException(nameof(raw));
        }

        public SecurityEventTokenPushResponse(EventTransmissionStatus status, HttpStatusCode httpStatusCode)
        {
            HttpStatusCode = httpStatusCode;
            Status = status;
            Raw = Array.Empty<byte>();
        }

        public SecurityEventTokenPushResponse(EventTransmissionStatus status)
        {
            Status = status;
            Raw = Array.Empty<byte>();
        }

        public HttpStatusCode? HttpStatusCode { get; }

        public EventTransmissionStatus Status { get; }

        public byte[] Raw { get; }

        public string? Error { get; private set; }

        public string? Description { get; private set; }

        public Exception? Exception { get; private set; }

        internal static async Task<SecurityEventTokenPushResponse> FromHttpResponseAsync(HttpResponseMessage responseMessage)
        {
            if (responseMessage.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                return Success(System.Net.HttpStatusCode.Accepted);
            }

            if (!string.Equals(responseMessage.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                return new SecurityEventTokenPushResponse(EventTransmissionStatus.Error, responseMessage.StatusCode);
            }

            var errorMessage = await responseMessage.Content.ReadAsByteArrayAsync();
            if (errorMessage == null)
            {
                // This should never occur.
                throw new InvalidOperationException("Null content.");
            }

            return ReadErrorMessage(responseMessage.StatusCode, errorMessage);
        }

        private static SecurityEventTokenPushResponse ReadErrorMessage(HttpStatusCode statusCode, byte[] errorMessage)
        {
            Utf8JsonReader reader = new Utf8JsonReader(errorMessage);
            try
            {
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
                                        return ErrorFailure(statusCode, errorMessage, err, description);
                                    }
                                    continue;
                                }

                                return ErrorFailure(statusCode, errorMessage, "parsing_error", "Error occurred during error message parsing: property 'err' must be of type 'string'.");

                            case "description":
                                if (reader.Read() && reader.TokenType == JsonTokenType.String)
                                {
                                    description = reader.GetString();
                                    if (err != null)
                                    {
                                        return ErrorFailure(statusCode, errorMessage, err, description);
                                    }

                                    continue;
                                }

                                return ErrorFailure(statusCode, errorMessage, "parsing_error", "Error occurred during error message parsing: property 'description' must be of type 'string'.");

                            default:
                                // Skip the unattended properties
                                reader.Skip();
                                continue;
                        }
                    }

                    if (err != null)
                    {
                        return ErrorFailure(statusCode, errorMessage, err, description);
                    }
                    else
                    {
                        return ErrorFailure(statusCode, errorMessage, "parsing_error", "Error occurred during error message parsing: missing property 'err'.");
                    }
                }
            }
            catch (JsonException e)
            {
                return Failure(statusCode, errorMessage, e);
            }

            return ErrorFailure(statusCode, errorMessage, "parsing_error", "Error occurred during error message parsing: invalid JSON." +
                Environment.NewLine + "The error message is:" +
                Environment.NewLine + Encoding.UTF8.GetString(errorMessage));
        }

        internal static SecurityEventTokenPushResponse Warning(Exception exception)
        {
            return new SecurityEventTokenPushResponse(EventTransmissionStatus.Warning)
            {
                Exception = exception
            };
        }

        internal static SecurityEventTokenPushResponse Warning(HttpStatusCode statusCode)
        {
            return new SecurityEventTokenPushResponse(EventTransmissionStatus.Warning, statusCode);
        }

        public static SecurityEventTokenPushResponse Success(HttpStatusCode statusCode)
        {
            return new SecurityEventTokenPushResponse(EventTransmissionStatus.Success, statusCode, Array.Empty<byte>());
        }

        public static SecurityEventTokenPushResponse Failure(HttpStatusCode statusCode, byte[] raw, Exception exception)
        {
            return new SecurityEventTokenPushResponse(EventTransmissionStatus.Error, statusCode, raw)
            {
                Exception = exception
            };
        }

        public static SecurityEventTokenPushResponse Failure(SecurityEventTokenPushResponse other)
        {
            if (other.HttpStatusCode.HasValue)
            {
                return new SecurityEventTokenPushResponse(EventTransmissionStatus.Error, other.HttpStatusCode.Value);
            }
            else
            {
                return new SecurityEventTokenPushResponse(EventTransmissionStatus.Error)
                {
                    Exception = other.Exception
                };
            }
        }

        public static SecurityEventTokenPushResponse Failure(HttpStatusCode statusCode)
        {
            return new SecurityEventTokenPushResponse(EventTransmissionStatus.Error, statusCode);
        }

        public static SecurityEventTokenPushResponse Failure(Exception exception)
        {
            return new SecurityEventTokenPushResponse(EventTransmissionStatus.Error, 0, Array.Empty<byte>())
            {
                Exception = exception
            };
        }

        public static SecurityEventTokenPushResponse ErrorFailure(HttpStatusCode statusCode, byte[] raw, string error, string? description = null)
        {
            return new SecurityEventTokenPushResponse(EventTransmissionStatus.Error, statusCode, raw)
            {
                Error = error,
                Description = description
            };
        }

        public static SecurityEventTokenPushResponse Warning(HttpStatusCode statusCode, byte[] raw, string error, string? description = null)
        {
            return new SecurityEventTokenPushResponse(EventTransmissionStatus.Warning, statusCode, raw)
            {
                Error = error,
                Description = description
            };
        }
    }
}