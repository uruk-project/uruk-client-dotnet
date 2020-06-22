using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using IdentityModel.Client;
using JsonWebToken;
using Microsoft.Extensions.Options;
using Xunit;

namespace Uruk.Client.Tests
{
    public class DefaultAccessTokenAcquisitorTests
    {
        [Fact]
        public async Task AcquireAccessToken_Success()
        {
            var tokenClient = new TokenClient(new HttpClient(new TestHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{
                   ""access_token"":""2YotnFZFEjr1zCsicMWpAA"",
                   ""token_type"":""example"",
                   ""expires_in"":3600,
                   ""refresh_token"":""tGzv3JOkF0XG5Qx2TlKWIA"",
                }")
            })), new TokenClientOptions() { Address = "https://example.com" });

            var acquisitor = new DefaultAccessTokenAcquisitor(new TestLogger<DefaultAccessTokenAcquisitor>(), tokenClient, Options.Create(new AuditTrailClientOptions()));
            var token = await acquisitor.AcquireAccessTokenAsync();

            Assert.Equal("2YotnFZFEjr1zCsicMWpAA", token);
        }

        [Fact]
        public async Task AcquireAccessToken_Twice_Success()
        {
            var tokenClient = new TokenClient(new HttpClient(new TestHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{
                   ""access_token"":""2YotnFZFEjr1zCsicMWpAA"",
                   ""token_type"":""example"",
                   ""expires_in"":3600,
                   ""refresh_token"":""tGzv3JOkF0XG5Qx2TlKWIA""
                }")
            })), new TokenClientOptions() { Address = "https://example.com" });

            var acquisitor = new DefaultAccessTokenAcquisitor(new TestLogger<DefaultAccessTokenAcquisitor>(), tokenClient, Options.Create(new AuditTrailClientOptions()));
            await acquisitor.AcquireAccessTokenAsync();
            var token = await acquisitor.AcquireAccessTokenAsync();

            // May also assert the http client is not used
            Assert.Equal("2YotnFZFEjr1zCsicMWpAA", token);
        }

        [Fact]
        public async Task AcquireAccessToken_HttpError_Fail()
        {
            var tokenClient = new TokenClient(new HttpClient(new TestHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new FailingHttpContent(typeof(HttpRequestException))
            })), new TokenClientOptions() { Address = "https://example.com" });

            var acquisitor = new DefaultAccessTokenAcquisitor(new TestLogger<DefaultAccessTokenAcquisitor>(), tokenClient, Options.Create(new AuditTrailClientOptions()));

            await Assert.ThrowsAsync<HttpRequestException>(() => acquisitor.AcquireAccessTokenAsync());
        }

        [Fact]
        public async Task AcquireAccessToken_ProtocolError_Fail()
        {
            var tokenClient = new TokenClient(new HttpClient(new TestHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(@"{
                   ""error"":""invalid_request""
                }")
            })), new TokenClientOptions() { Address = "https://example.com" });

            var acquisitor = new DefaultAccessTokenAcquisitor(new TestLogger<DefaultAccessTokenAcquisitor>(), tokenClient, Options.Create(new AuditTrailClientOptions()));

            await Assert.ThrowsAsync<HttpRequestException>(() => acquisitor.AcquireAccessTokenAsync());
        }
    }

    public class DefaultAuditTrailStoreTests : IDisposable
    {
        private readonly string _directory;

        public DefaultAuditTrailStoreTests()
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _directory = Path.Combine(root, Constants.DefaultStorageDirectory, Guid.NewGuid().ToString());
        }

        [Fact]
        public async Task RecordToken_NewFileInDirectory()
        {
            string path = Path.Combine(_directory, nameof(RecordToken_NewFileInDirectory));
            var initialFileCount = EnumerateFiles(path).Count();
            var store = CreateStore(path);
            await store.RecordAuditTrailAsync(new byte[] { 0x01, 0x02, 0x03, 0x04 });
            var finalFileCount = EnumerateFiles(path).Count();

            Assert.Equal(initialFileCount + 1, finalFileCount);

        }

        [Fact]
        public void GetAllTokens_IterateAllTokenFiles()
        {
            const string jwe = "eyJhbGciOiJkaXIiLCJlbmMiOiJBMTI4Q0JDLUhTMjU2IiwiemlwIjoiREVGIn0..WJyI8eJZEgsU890A34fKSg.UePAIdDFOnEVx-6TeLm-KQ.IzfCGcPMXwZRnU_NRlAfc-lW18s1w9UqPzAYto_21gw";

            string path = Path.Combine(_directory, nameof(GetAllTokens_IterateAllTokenFiles));
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "file1.token"), jwe);
            File.WriteAllText(Path.Combine(path, "file2.token"), jwe);
            File.WriteAllText(Path.Combine(path, "file3.token"), jwe);
            File.WriteAllText(Path.Combine(path, "file_invalid.token"), "eyJhbGciOiJkaXIiLCJlbmMiOiJBMTI4Q0JDLUhTMjU2IiwiemlwIjoiREVGIn0..WJyI8eJZEgsU890A34fKSg.UePAIdDFOnEVx-6TeLm-KQ.IzfCGcPMXwZRnU_NRlAfc-lW18s1w9UqPzAYto_21gwXXX");
            var store = CreateStore(path);
            int i = 0;
            foreach (var token in store.GetAllAuditTrailRecords())
            {
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, token.Value);
                i++;
            }

            Assert.Equal(3, i);
        }

        private static DefaultAuditTrailStore CreateStore(string path)
        {
            return new DefaultAuditTrailStore(Options.Create(new AuditTrailClientOptions
            {
                TemporaryStorageEncryptionKey = new SymmetricJwk(new byte[32]),
                TemporaryStoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path)
            }), new TestLogger<DefaultAuditTrailStore>());
        }

        public void Dispose()
        {
            Directory.Delete(_directory, true);
        }

        private IEnumerable<string> EnumerateFiles(string? path = null)
        {
            var directory = path is null ? _directory : path;
            if (!Directory.Exists(directory))
            {
                yield break;
            }

            foreach (var item in Directory.EnumerateFiles(directory, "*.token", SearchOption.AllDirectories))
            {
                yield return item;
            }
        }
    }
}
