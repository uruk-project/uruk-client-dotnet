using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
#if NETSTANDARD2_0 || NETSTANDARD2_1
using System.Collections.Concurrent;
#else
using System.Threading.Channels;
#endif

namespace Uruk.Client
{
    internal class InMemoryTokenSink : ITokenSink
    {
        private readonly ILogger<InMemoryTokenSink> _logger;
#if NETSTANDARD2_0 || NETSTANDARD2_1
        private readonly BlockingCollection<Token> _channel = new BlockingCollection<Token>();
#else
        private readonly Channel<Token> _channel =  Channel.CreateUnbounded<Token>();
#endif
        public InMemoryTokenSink(ILogger<InMemoryTokenSink> logger)
        {
            _logger = logger;
        }

#if NETSTANDARD2_0 || NETSTANDARD2_1
        public bool TryWrite(Token token)
        {
            return _channel.TryAdd(token);
        }

        public Task Flush(ISecurityEventTokenClient client, CancellationToken cancellationToken)
        {
            Task consumer = Task.Factory.StartNew(() =>
            {
                while (_channel.TryTake(out var token))
                {
                    _logger.LogInformation(Encoding.UTF8.GetString(token.Value));
                    client.SendTokenAsync(token.Value);
                }
            });

            return consumer;
        }
#else
        public bool TryWrite(Token token)
        {
            return _channel.Writer.TryWrite(token);
        }

        public Task Flush(ISecurityEventTokenClient client, CancellationToken cancellationToken)
        {
            Task consumer = Task.Factory.StartNew(async () =>
            {
                while (await _channel.Reader.WaitToReadAsync())
                {
                    while (_channel.Reader.TryRead(out var token))
                    {
                        // TODO: creates a logger message
                        _logger.LogInformation(Encoding.UTF8.GetString(token.Value));
                        client.SendTokenAsync(token.Value);
                    }
                }
            });

            return consumer;
        }
#endif
    }
}