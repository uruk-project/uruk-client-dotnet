using System;
using System.Net;
using JsonWebToken;
using Microsoft.Extensions.Logging;

internal static class LoggingExtensions
{
    private static readonly Action<ILogger, string, Exception?> _usingEphemeralFileSystemLocationInContainer;
    private static readonly Action<ILogger, string, Exception?> _writingTokenToFile;
    private static readonly Action<ILogger, string, TokenValidationStatus, Exception?> _readingTokenFileFailed;
    private static readonly Action<ILogger, string, Exception?> _readingTokenFileFailed2;

    private static readonly Action<ILogger, string, HttpStatusCode, Exception?> _sendingTokenFailed;
    private static readonly Action<ILogger, string, HttpStatusCode, Exception?> _sendingTokenFailedNoRetry;
    private static readonly Action<ILogger, string, Exception?> _sendingTokenFailedOnRequest;
    private static readonly Action<ILogger, string, Exception?> _sendingTokenFailedOnRequestNoRetry;

    static LoggingExtensions()
    {
        _usingEphemeralFileSystemLocationInContainer = LoggerMessage.Define<string>(
            eventId: new EventId(1, "UsingEphemeralFileSystemLocationInContainer"),
            logLevel: LogLevel.Warning,
            formatString: "Storing tokens in a directory '{path}' that may not be persisted outside of the container. Tokens will be unavailable when container is destroyed.");

        _writingTokenToFile = LoggerMessage.Define<string>(
          eventId: new EventId(1, "WritingTokenToFile"),
          logLevel: LogLevel.Information,
          formatString: "Writing token to file '{FileName}'.");

        _readingTokenFileFailed = LoggerMessage.Define<string, TokenValidationStatus>(
          eventId: new EventId(3, "ReadingTokenFileFailed"),
          logLevel: LogLevel.Warning,
          formatString: "Reading token file '{FileName}' has failed: {Status}.");

        _readingTokenFileFailed2 = LoggerMessage.Define<string>(
          eventId: new EventId(3, "ReadingTokenFileFailed"),
          logLevel: LogLevel.Warning,
          formatString: "Reading token file '{FileName}' has failed.");

        _sendingTokenFailed = LoggerMessage.Define<string, HttpStatusCode>(
          eventId: new EventId(4, "SendingTokenFailedNoRetry"),
          logLevel: LogLevel.Warning,
          formatString: "An error occurred when trying to send token to {RequestUri}. The server replied with HTTP status code {StatusCode}. The token will be sent again later.");

        _sendingTokenFailedNoRetry = LoggerMessage.Define<string, HttpStatusCode>(
          eventId: new EventId(5, "SendingTokenFailed"),
          logLevel: LogLevel.Error,
          formatString: "An error occurred when trying to send token to {RequestUri}. The server replied with HTTP status code {StatusCode}.");
    
        _sendingTokenFailedOnRequest = LoggerMessage.Define<string>(
          eventId: new EventId(6, "SendingTokenFailedNoRetry"),
          logLevel: LogLevel.Warning,
          formatString: "An error occurred when trying to send token to {RequestUri}. The token will be sent again later.");

        _sendingTokenFailedOnRequestNoRetry = LoggerMessage.Define<string>(
          eventId: new EventId(7, "SendingTokenFailed"),
          logLevel: LogLevel.Error,
          formatString: "An error occurred when trying to send token to {RequestUri}.");
    }

    public static void UsingEphemeralFileSystemLocationInContainer(this ILogger logger, string path)
    {
        _usingEphemeralFileSystemLocationInContainer(logger, path, null);
    }

    public static void WritingTokenToFile(this ILogger logger, string finalFilename)
    {
        _writingTokenToFile(logger, finalFilename, null);
    }

    public static void ReadingTokenFileFailed(this ILogger logger, string filename, TokenValidationStatus status, Exception? e = null)
    {
        _readingTokenFileFailed(logger, filename, status, e);
    }
    public static void ReadingTokenFileFailed(this ILogger logger, string filename, Exception? e = null)
    {
        _readingTokenFileFailed2(logger, filename, e);
    }

    public static void SendingTokenFailedNoRetry(this ILogger logger, string requestUri, HttpStatusCode? statusCode = null, Exception? exception = null)
    {
        if (statusCode.HasValue)
        {
            _sendingTokenFailedNoRetry(logger, requestUri, statusCode.Value, exception);
        }
        else
        {
            _sendingTokenFailedOnRequestNoRetry(logger, requestUri, exception);
        }
    }

    public static void SendingTokenFailed(this ILogger logger, string requestUri, HttpStatusCode? statusCode = null, Exception? exception = null)
    {
        if (statusCode.HasValue)
        {
            _sendingTokenFailed(logger, requestUri, statusCode.Value, exception);
        }
        else
        {
            _sendingTokenFailedOnRequest(logger, requestUri, exception);
        }
    }
}
