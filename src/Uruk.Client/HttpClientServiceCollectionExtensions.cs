using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Uruk.Client
{
    /// <summary>
    /// Provides extension methods for registering <see cref="EventReceiverService"/> in an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class HttpClientServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the <see cref="SecurityEventTokenClient"/> to the container, using the provided delegate to register
        /// event receiver.
        /// </summary>
        /// <remarks>
        /// This operation is idempotent - multiple invocations will still only result in a single
        /// <see cref="SecurityEventTokenClient"/> instance in the <see cref="IServiceCollection"/>. It can be invoked
        /// multiple times in order to get access to the <see cref="IEventReceiverBuilder"/> in multiple places.
        /// </remarks>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the <see cref="EventReceiverService"/> to.</param>
        /// <param name="audience">The audience.</param>
        /// <returns>An instance of <see cref="IEventReceiverBuilder"/> from which event receiver can be registered.</returns>
        public static IHttpClientBuilder AddSecurityEventTokenClient(this IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddHostedService<TokenRetryBackgroundService>();
            services.AddHostedService<TokenRecoveryService>();
            services.TryAddSingleton<ITokenSink, DefaultTokenSink>();
            services.TryAddSingleton<ITokenStore, DefaultTokenStore>();
            return services.AddHttpClient<ISecurityEventTokenClient, SecurityEventTokenClient>();
        }

        public static IHttpClientBuilder AddSecurityEventTokenClient(this IServiceCollection services, Action<SecurityEventTokenClientOptions> configure)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            services.Configure(configure);
            return services.AddSecurityEventTokenClient();
        }

        public static IServiceCollection ConfigureSecurityEventTokenClient(this IServiceCollection services, Action<SecurityEventTokenClientOptions> configure)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            services.Configure(configure);
            return services;
        }
    }
}