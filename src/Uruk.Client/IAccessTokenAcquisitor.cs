using System.Threading;
using System.Threading.Tasks;

namespace Uruk.Client
{
    public interface IAccessTokenAcquirer
    {
        Task<string?> AcquireAccessTokenAsync(CancellationToken cancellationToken = default);
    }
}