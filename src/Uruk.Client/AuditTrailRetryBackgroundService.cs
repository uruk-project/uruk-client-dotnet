using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Uruk.Client
{
    public class AuditTrailRetryBackgroundService : BackgroundService
    {
        private readonly IAuditTrailClient _client;
        private readonly IAuditTrailSink _sink;
        private readonly IAuditTrailStore _store;
        private readonly ILogger<AuditTrailRetryBackgroundService> _logger;

        public AuditTrailRetryBackgroundService(IAuditTrailClient client, IAuditTrailSink sink, IAuditTrailStore store, ILogger<AuditTrailRetryBackgroundService> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (await _sink.WaitToReadAsync(cancellationToken))
            {
                while (_sink.TryRead(out var token))
                {
                    token.RetryCount++;
                    _logger.LogInformation($"'{Encoding.UTF8.GetString(token.Value)}', try #{token.RetryCount}");
                    if (token.RetryCount < 3)
                    {
                        var response = await _client.ResendAuditRrailAsync(token, cancellationToken);
                        if (response.Status == EventTransmissionStatus.Success)
                        {
                            _store.DeleteRecord(token);
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"'{Encoding.UTF8.GetString(token.Value)}', try #{token.RetryCount}");
                    }
                }
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return _sink.Stop(cancellationToken);
        }
    }
}