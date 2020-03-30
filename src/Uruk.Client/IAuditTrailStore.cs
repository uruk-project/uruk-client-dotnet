using System.Collections.Generic;
using System.Threading.Tasks;

namespace Uruk.Client
{
    public interface IAuditTrailStore
    {
        Task<string> RecordAuditTrailAsync(byte[] token);

        IEnumerable<Token> GetAllAuditTrailRecords();

        void DeleteRecord(Token token);
    }
}
