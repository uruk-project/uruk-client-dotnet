using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Uruk.Client.Tests
{
    internal class TestTokenStore : IAuditTrailStore
    {
        private readonly AuditTrailItem[] _auditTrails;

        public TestTokenStore(AuditTrailItem[]? auditTrails = null)
        {
            _auditTrails = auditTrails ?? Array.Empty<AuditTrailItem>();
        }

        public int RecordedCount { get; set; }

        public void DeleteRecord(AuditTrailItem token)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<AuditTrailItem> GetAllAuditTrailRecords()
        {
            return _auditTrails;
        }

        public Task<string> RecordAuditTrailAsync(byte[] token)
        {
            RecordedCount++;
            return Task.FromResult<string>(string.Empty);
        }
    }
}
