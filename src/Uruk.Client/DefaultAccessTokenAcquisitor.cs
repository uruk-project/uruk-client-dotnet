using System;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using IdentityModel.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Uruk.Client
{
    public sealed class DefaultAccessTokenAcquisitor : IAccessTokenAcquisitor
    {
        private readonly ILogger<DefaultAccessTokenAcquisitor> _logger;
        private readonly TokenClient _tokenClient;
        private readonly AuditTrailClientOptions _options;

        private string? _accessToken;
        private DateTime _accessTokenExpireAt = DateTime.MinValue;

        public DefaultAccessTokenAcquisitor(ILogger<DefaultAccessTokenAcquisitor> logger, TokenClient? tokenClient, IOptions<AuditTrailClientOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tokenClient = tokenClient ?? throw new ArgumentNullException(nameof(tokenClient));
            _options = options.Value;
        }

        public async Task<string?> AcquireAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            if (DateTime.UtcNow >= _accessTokenExpireAt)
            {
                var response = await _tokenClient.RequestClientCredentialsTokenAsync(scope: _options.AccessTokenScope, cancellationToken: cancellationToken);
                if (!response.IsError)
                {
                    _accessToken = response.AccessToken;
                    _accessTokenExpireAt = DateTime.UtcNow.AddSeconds(response.ExpiresIn);
                }
                else
                {
                    if (response.ErrorType == ResponseErrorType.Exception)
                    {
#if NETSTANDARD2_0
                        ExceptionDispatchInfo.Capture(response.Exception).Throw();
                        _accessToken = string.Empty;
#else
                        ExceptionDispatchInfo.Throw(response.Exception);
#endif
                        _logger.LogError("Access token acquisition failed.", response.Exception);
                    }
                    else
                    {
                        _logger.LogError("Access token acquisition failed. {error} - {description}", response.Error, response.ErrorDescription);
                        throw new HttpRequestException($"{response.Error} - {response.ErrorDescription}");
                    }
                }
            }

            return _accessToken;
        }
    }
}