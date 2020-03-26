using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Uruk.Client
{
    public class TokenRecoveryService : IHostedService, IDisposable
    {
        private readonly ILogger<TokenRecoveryService> _logger;
        private readonly ITokenStore _store;
        private readonly ITokenSink _sink;
        private Timer? _timer;

        public TokenRecoveryService(ILogger<TokenRecoveryService> logger, ITokenStore store, ITokenSink sink)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Timed Hosted Service running.");
            _timer = new Timer(Process, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));

            return Task.CompletedTask;
        }

        private void Process(object? state)
        {
            _logger.LogInformation("Starting {TaskName} task ...", nameof(TokenRecoveryService));
            int count = 0;
            foreach (var token in _store.GetAllTokenRecords())
            {
                if (!_sink.TryWrite(token))
                {
                    _logger.LogWarning("Task {TaskName} aborted. The sink is completed. {Count} token(s) injected.", nameof(TokenRecoveryService));
                    return;
                }

                count++;
            }

            _logger.LogInformation("Task {TaskName} completed. {Count} token(s) injected.", nameof(TokenRecoveryService), count);
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