using System.Collections.Generic;
using System.Threading.Tasks;

namespace Uruk.Client
{
    public interface IAuditTrailStore
    {
        Task<string> RecordAuditTrailAsync(byte[] token);

        IEnumerable<AuditTrailItem> GetAllAuditTrailRecords();

        void DeleteRecord(AuditTrailItem token);
    }
}
