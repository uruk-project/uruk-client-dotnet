using System.Collections.Generic;
using System.Threading.Tasks;

namespace Uruk.Client
{
    public interface ITokenStore
    {
        Task<string> RecordTokenAsync(byte[] token);

        IEnumerable<Token> GetAllTokenRecords();

        void DeleteRecord(Token token);
    }
}
