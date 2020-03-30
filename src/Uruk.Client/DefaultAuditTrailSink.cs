using System.Threading;
using System.Threading.Tasks;
#if NETSTANDARD2_0 || NETSTANDARD2_1
using System.Collections.Concurrent;
#else
using System.Threading.Channels;
#endif

namespace Uruk.Client
{
    internal class DefaultAuditTrailSink : IAuditTrailSink
    {
#if NETSTANDARD2_0 || NETSTANDARD2_1
        private readonly BlockingCollection<AuditTrailItem> _channel = new BlockingCollection<AuditTrailItem>();

        public bool TryWrite(AuditTrailItem token)
        {
            return _channel.TryAdd(token);
        }

        public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ValueTask<bool>(Task.FromCanceled<bool>(cancellationToken));
            }

            return new ValueTask<bool>(Task.FromResult(true));
        }

        public bool TryRead(out AuditTrailItem token)
        {
            return _channel.TryTake(out token);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _channel.CompleteAdding();
            return Task.Delay(100);
        }
#else
        private readonly Channel<AuditTrailItem> _channel = Channel.CreateUnbounded<AuditTrailItem>();
        public bool TryWrite(AuditTrailItem token)
        {
            return _channel.Writer.TryWrite(token);
        }

        public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
        {
            return _channel.Reader.WaitToReadAsync(cancellationToken);
        }

        public bool TryRead(out AuditTrailItem token)
        {
            return _channel.Reader.TryRead(out token);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _channel.Writer.Complete();
            return _channel.Reader.Completion;
        }
#endif
    }
}