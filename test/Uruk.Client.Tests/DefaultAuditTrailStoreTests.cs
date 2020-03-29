using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Xunit;

namespace Uruk.Client.Tests
{
    public class DefaultAuditTrailStoreTests : IDisposable
    {
        private readonly string _directory;

        public DefaultAuditTrailStoreTests()
        {
            const string tokensFallbackDir = "SET_TOKENS_FALLBACK_DIR";
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                        ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                        ?? Environment.GetEnvironmentVariable(tokensFallbackDir);

            _directory = Path.Combine(root, ".uruk");
        }

        [Fact]
        public async Task RecordToken_NewFileInDirectory()
        {
            var initialFileCount = GetTokenFiles().Length;
            var store = CreateStore();
            await store.RecordAudirTrailAsync(new byte[] { 0x01, 0x02, 0x03, 0x04 });
            var finalFileCount = GetTokenFiles().Length;

            Assert.Equal(initialFileCount + 1, finalFileCount);
        }

        [Fact]
        public void GetAllTokens_IterateAllTokenFiles()
        {
            const string jwe = "eyJhbGciOiJkaXIiLCJlbmMiOiJBMTI4Q0JDLUhTMjU2IiwiemlwIjoiREVGIn0..WJyI8eJZEgsU890A34fKSg.UePAIdDFOnEVx-6TeLm-KQ.IzfCGcPMXwZRnU_NRlAfc-lW18s1w9UqPzAYto_21gw";

            File.WriteAllText(Path.Combine(_directory, "file1.token"), jwe);
            File.WriteAllText(Path.Combine(_directory, "file2.token"), jwe);
            File.WriteAllText(Path.Combine(_directory, "file3.token"), jwe);
            File.WriteAllText(Path.Combine(_directory, "file_invalid.token"), "eyJhbGciOiJkaXIiLCJlbmMiOiJBMTI4Q0JDLUhTMjU2IiwiemlwIjoiREVGIn0..WJyI8eJZEgsU890A34fKSg.UePAIdDFOnEVx-6TeLm-KQ.IzfCGcPMXwZRnU_NRlAfc-lW18s1w9UqPzAYto_21gwXXX");
            var store = CreateStore();
            int i = 0;
            foreach (var token in store.GetAllAuditTrailRecords())
            {
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, token.Value);
                i++;
            }

            Assert.Equal(3, i);
        }

        private static DefaultAuditTrailStore CreateStore()
        {
            return new DefaultAuditTrailStore(Options.Create(new AuditTrailClientOptions { EncryptionKey = new byte[32] }), new TestLogger<DefaultAuditTrailStore>());
        }

        public void Dispose()
        {
            foreach (var filename in GetTokenFiles())
            {
                File.Delete(filename);
            }
        }

        private string[] GetTokenFiles()
        {
            if (!Directory.Exists(_directory))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(_directory, "*.token", SearchOption.TopDirectoryOnly);
        }
    }
}
