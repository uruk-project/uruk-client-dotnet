using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Uruk.Client
{
    public class TokenSinkBackgroundService : BackgroundService
    {
        private readonly ISecurityEventTokenClient _client;
        private readonly ITokenSink _sink;

        public TokenSinkBackgroundService(ISecurityEventTokenClient client, ITokenSink sink)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return _sink.Flush(_client, stoppingToken);
        }
    }
}