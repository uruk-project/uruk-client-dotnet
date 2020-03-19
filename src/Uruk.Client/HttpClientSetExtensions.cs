using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Uruk.Client
{
    /// <summary>
    /// HttpClient extensions for security event token requests
    /// </summary>
    public static class HttpClientSetExtensions
    {
        /// <summary>
        /// Sends a token request .
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public static async Task<SecurityEventTokenPushResponse> SendTokenAsync(this HttpMessageInvoker client, SecurityEventTokenPushRequest request, CancellationToken cancellationToken = default)
        {
            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, cancellationToken);
                return await SecurityEventTokenPushResponse.FromHttpResponseAsync(response);
            }
            catch (OperationCanceledException exception)
            {
                return await Retry(client, request, exception, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                // Timeout...
                // TODO: Retry
                // TODO: Warning & persist event for later 
                return await Retry(client, request, exception, cancellationToken);
            }
            catch (Exception exception)
            {
                return SecurityEventTokenPushResponse.Failure(exception);
            }
        }

        private static async Task<SecurityEventTokenPushResponse> Retry(HttpMessageInvoker client, SecurityEventTokenPushRequest failedRequest, Exception exception, CancellationToken cancellationToken)
        {
            var retryMessage = new SecurityEventTokenPushRequest(failedRequest);
            HttpResponseMessage response;
            try
            {
                await Task.Delay(failedRequest.RetryDelay);
                response = await client.SendAsync(retryMessage, cancellationToken);
            }
            catch (Exception retryException)
            {
                return SecurityEventTokenPushResponse.Failure(new AggregateException(exception, retryException));
            }

            return await SecurityEventTokenPushResponse.FromHttpResponseAsync(response);
        }
    }
}