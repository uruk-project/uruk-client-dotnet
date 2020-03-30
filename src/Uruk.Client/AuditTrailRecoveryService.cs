using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Uruk.Client
{
    public class AuditTrailRecoveryService : IHostedService, IDisposable
    {
        private readonly AuditTrailClientOptions _options;
        private readonly ILogger<AuditTrailRecoveryService> _logger;
        private readonly IAuditTrailStore _store;
        private readonly IAuditTrailSink _sink;
        private Timer? _timer;

        public AuditTrailRecoveryService(IOptions<AuditTrailClientOptions> options, ILogger<AuditTrailRecoveryService> logger, IAuditTrailStore store, IAuditTrailSink sink)
        {
            _options = options.Value;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Timed Hosted Service running.");
            _timer = new Timer(Process, null, TimeSpan.Zero, TimeSpan.FromSeconds(_options.ResendIntervalInSeconds));

            return Task.CompletedTask;
        }

        private void Process(object? state)
        {
            _logger.LogInformation("Starting {TaskName} task ...", nameof(AuditTrailRecoveryService));
            int count = 0;
            foreach (var token in _store.GetAllAuditTrailRecords())
            {
                if (!_sink.TryWrite(token))
                {
                    _logger.LogWarning("Task {TaskName} aborted. The sink is completed. {Count} token(s) injected.", nameof(AuditTrailRecoveryService));
                    return;
                }

                count++;
            }

            _logger.LogInformation("Task {TaskName} completed. {Count} token(s) injected.", nameof(AuditTrailRecoveryService), count);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Timed Hosted Service is stopping.");
            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}