using System.Threading;
using System.Threading.Tasks;

namespace Uruk.Client
{
    public interface IAuditTrailSink
    {
        public bool TryWrite(AuditTrailItem token);

        public Task StopAsync(CancellationToken cancellationToken);

        public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken);

        public bool TryRead(out AuditTrailItem token);
    }
}