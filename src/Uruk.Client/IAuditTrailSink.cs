using System.Threading;
using System.Threading.Tasks;

namespace Uruk.Client
{
    public interface IAuditTrailSink
    {
        public bool TryWrite(Token token);

        public Task Stop(CancellationToken cancellationToken);

        public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken);

        public bool TryRead(out Token token);
    }
}