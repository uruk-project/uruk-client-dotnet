using System.Threading;
using System.Threading.Tasks;

namespace Uruk.Client
{
    public interface ITokenSink
    {
        public bool TryWrite(Token token);

        public Task Flush(ISecurityEventTokenClient client, CancellationToken cancellationToken);
    }
}