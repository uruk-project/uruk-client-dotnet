using System.Threading;
using System.Threading.Tasks;

namespace Uruk.Client
{
    public interface IAccessTokenAcquisitor
    {
        Task<string?> AcquireAccessTokenAsync(CancellationToken cancellationToken = default);
    }
}