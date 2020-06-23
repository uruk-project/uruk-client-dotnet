using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Xunit;

namespace Uruk.Client.Tests
{
    public class AuditTrailRecoveryServiceTests
    {
        [Fact]
        public void Ctor()
        {
            IOptions<AuditTrailClientOptions> options = Options.Create(new AuditTrailClientOptions());
            TestSink sink = new TestSink(true);
            TestTokenStore store = new TestTokenStore(null);
            TestLogger<AuditTrailRecoveryService> testLogger = new TestLogger<AuditTrailRecoveryService>();
            var service = new AuditTrailRecoveryService(options, testLogger, store, sink);

            Assert.Throws<ArgumentNullException>("options", () => new AuditTrailRecoveryService(null!, testLogger, store, sink));
            Assert.Throws<ArgumentNullException>("logger", () => new AuditTrailRecoveryService(options, null!, store, sink));
            Assert.Throws<ArgumentNullException>("store", () => new AuditTrailRecoveryService(options, testLogger, null!, sink));
            Assert.Throws<ArgumentNullException>("sink", () => new AuditTrailRecoveryService(options, testLogger, store, null!));
        }

        [Theory]
        [InlineData(true, 3)]
        [InlineData(false, 0)]
        public async Task Test(bool canWrite, int expectedCount)
        {
            var options = new AuditTrailClientOptions
            {
                ResendIntervalInSeconds = 0
            };
            var items = new[] { new AuditTrailItem(), new AuditTrailItem(), new AuditTrailItem() };
            TestSink sink = new TestSink(canWrite);
            TestTokenStore store = new TestTokenStore(items);
            var service = new AuditTrailRecoveryService(Options.Create(options), new TestLogger<AuditTrailRecoveryService>(), store, sink);

            await service.StartAsync(default);
            await Task.Delay(100);
            Assert.Equal(expectedCount, sink.Items.Count);
        }

        private class TestSink : IAuditTrailSink
        {
            public List<AuditTrailItem> Items { get; } = new List<AuditTrailItem>();
            private readonly bool _canWrite;

            public TestSink(bool canWrite)
            {
                _canWrite = canWrite;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public bool TryRead(out AuditTrailItem token)
            {
                throw new NotImplementedException();
            }

            public bool TryWrite(AuditTrailItem token)
            {
                if (_canWrite)
                {
                    Items.Add(token);
                }

                return _canWrite;
            }

            public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
