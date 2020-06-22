using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Uruk.Client
{
    public class AuditTrailPushResponse
    {
        public AuditTrailPushResponse(EventTransmissionStatus status, HttpStatusCode httpStatusCode, string body)
        {
            HttpStatusCode = httpStatusCode;
            Status = status;
            ErrorBody = body ?? throw new ArgumentNullException(nameof(body));
        }

        public AuditTrailPushResponse(EventTransmissionStatus status, HttpStatusCode httpStatusCode)
        {
            HttpStatusCode = httpStatusCode;
            Status = status;
        }

        public AuditTrailPushResponse(EventTransmissionStatus status)
        {
            Status = status;
        }

        public EventTransmissionStatus Status { get; }

        public HttpStatusCode? HttpStatusCode { get; }

        public string? ErrorBody { get; }

        public string? Error { get; private set; }

        public string? ErrorDescription { get; private set; }

        public Exception? Exception { get; private set; }

        internal static async Task<AuditTrailPushResponse> FromHttpResponseAsync(HttpResponseMessage responseMessage)
        {
            if (responseMessage.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                return Success(System.Net.HttpStatusCode.Accepted);
            }

            if (!string.Equals(responseMessage.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                return new AuditTrailPushResponse(EventTransmissionStatus.Error, responseMessage.StatusCode);
            }

            var errorMessage = await responseMessage.Content.ReadAsByteArrayAsync();
            if (errorMessage == null)
            {
                // This should never occur.
                throw new InvalidOperationException("Null content.");
            }

            return ReadErrorMessage(responseMessage.StatusCode, errorMessage);
        }

        private static AuditTrailPushResponse ReadErrorMessage(HttpStatusCode statusCode, byte[] errorMessage)
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
                                        return ErrorFailure(statusCode, Encoding.UTF8.GetString(errorMessage), err, description);
                                    }
                                    continue;
                                }

                                return ErrorFailure(statusCode, Encoding.UTF8.GetString(errorMessage), "parsing_error", "Error occurred during error message parsing: property 'err' must be of type 'string'.");

                            case "description":
                                if (reader.Read() && reader.TokenType == JsonTokenType.String)
                                {
                                    description = reader.GetString();
                                    if (err != null)
                                    {
                                        return ErrorFailure(statusCode, Encoding.UTF8.GetString(errorMessage), err, description);
                                    }

                                    continue;
                                }

                                return ErrorFailure(statusCode, Encoding.UTF8.GetString(errorMessage), "parsing_error", "Error occurred during error message parsing: property 'description' must be of type 'string'.");

                            default:
                                // Skip the unattended properties
                                reader.Skip();
                                continue;
                        }
                    }

                    if (err != null)
                    {
                        return ErrorFailure(statusCode, Encoding.UTF8.GetString(errorMessage), err, description);
                    }
                    else
                    {
                        return ErrorFailure(statusCode, Encoding.UTF8.GetString(errorMessage), "parsing_error", "Error occurred during error message parsing: missing property 'err'.");
                    }
                }
            }
            catch (JsonException e)
            {
                return Failure(statusCode, Encoding.UTF8.GetString(errorMessage), e);
            }

            return ErrorFailure(statusCode, Encoding.UTF8.GetString(errorMessage), "parsing_error", "Error occurred during error message parsing: invalid JSON." +
                Environment.NewLine + "The error message is:" +
                Environment.NewLine + Encoding.UTF8.GetString(errorMessage));
        }

        internal static AuditTrailPushResponse ShouldRetry(Exception exception)
        {
            return new AuditTrailPushResponse(EventTransmissionStatus.ShouldRetry)
            {
                Exception = exception
            };
        }

        internal static AuditTrailPushResponse ShouldRetry(HttpStatusCode statusCode)
        {
            return new AuditTrailPushResponse(EventTransmissionStatus.ShouldRetry, statusCode);
        }

        public static AuditTrailPushResponse Success(HttpStatusCode statusCode)
        {
            return new AuditTrailPushResponse(EventTransmissionStatus.Success, statusCode);
        }

        public static AuditTrailPushResponse Failure(HttpStatusCode statusCode, string body, Exception exception)
        {
            return new AuditTrailPushResponse(EventTransmissionStatus.Error, statusCode, body)
            {
                Exception = exception
            };
        }

        public static AuditTrailPushResponse Failure(AuditTrailPushResponse other)
        {
            if (other.HttpStatusCode.HasValue)
            {
                return new AuditTrailPushResponse(EventTransmissionStatus.Error, other.HttpStatusCode.Value);
            }
            else
            {
                return new AuditTrailPushResponse(EventTransmissionStatus.Error)
                {
                    Exception = other.Exception
                };
            }
        }

        public static AuditTrailPushResponse Failure(HttpStatusCode statusCode)
        {
            return new AuditTrailPushResponse(EventTransmissionStatus.Error, statusCode);
        }

        public static AuditTrailPushResponse Failure(Exception exception)
        {
            return new AuditTrailPushResponse(EventTransmissionStatus.Error, 0)
            {
                Exception = exception
            };
        }
        
        public static AuditTrailPushResponse TokenAcquisitionFailure(Exception exception)
        {
            return new AuditTrailPushResponse(EventTransmissionStatus.TokenAcquisitionError)
            {
                Exception = exception
            };
        }

        public static AuditTrailPushResponse ErrorFailure(HttpStatusCode statusCode, string body, string error, string? description = null)
        {
            return new AuditTrailPushResponse(EventTransmissionStatus.Error, statusCode, body)
            {
                Error = error,
                ErrorDescription = description
            };
        }

        public static AuditTrailPushResponse ShouldRetry(HttpStatusCode statusCode, string body, string error, string? description = null)
        {
            return new AuditTrailPushResponse(EventTransmissionStatus.ShouldRetry, statusCode, body)
            {
                Error = error,
                ErrorDescription = description
            };
        }
    }
}