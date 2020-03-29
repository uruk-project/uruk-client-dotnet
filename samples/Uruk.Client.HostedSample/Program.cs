using JsonWebToken;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Uruk.Client.HostedSample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services
                        .AddHostedService<Worker>()
                        .AddAuditTrailClient(o =>
                        {
                            o.DeliveryEndpoint = "https://example.com/events/";
                            o.StorageEncryptionKey = new SymmetricJwk(new byte[32]);
                        });
                });
        }
    }
}
