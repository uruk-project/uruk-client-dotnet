using System;
using IdentityModel.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Uruk.Client
{
    /// <summary>
    /// Provides extension methods for registering <see cref="EventReceiverService"/> in an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class HttpClientServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the <see cref="AuditTrailClient"/> to the container, using the provided delegate to register
        /// event receiver.
        /// </summary>
        /// <remarks>
        /// This operation is idempotent - multiple invocations will still only result in a single
        /// <see cref="AuditTrailClient"/> instance in the <see cref="IServiceCollection"/>. It can be invoked
        /// multiple times in order to get access to the <see cref="IHttpClientBuilder"/> in multiple places.
        /// </remarks>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the <see cref="EventReceiverService"/> to.</param>
        /// <param name="audience">The audience.</param>
        /// <returns>An instance of <see cref="IHttpClientBuilder"/> from which event receiver can be registered.</returns>
        private static IHttpClientBuilder AddAuditTrailClient(this IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }   

            services.AddHostedService<AuditTrailRetryBackgroundService>();
            services.AddHostedService<AuditTrailRecoveryService>();
            services.TryAddSingleton<IAuditTrailSink, DefaultAuditTrailSink>();
            services.TryAddSingleton<IAuditTrailStore, DefaultAuditTrailStore>();
            services.TryAddSingleton<IAccessTokenAcquirer, DefaultAccessTokenAcquirer>();

            services.AddOptions<AuditTrailClientOptions>();
            services.AddTransient(sp => sp.GetRequiredService<IOptions<AuditTrailClientOptions>>().Value.TokenClientOptions);
            services.AddHttpClient<TokenClient>();

            return services.AddHttpClient<IAuditTrailClient, AuditTrailClient>();
        }

        public static IHttpClientBuilder AddAuditTrailClient(this IServiceCollection services, Action<AuditTrailClientOptions> configure)
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
            return services.AddAuditTrailClient();
        }

        public static IServiceCollection ConfigureAuditTrailClient(this IServiceCollection services, Action<AuditTrailClientOptions> configure)
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