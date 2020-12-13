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
                        .AddAuditTrailClient(options =>
                        {
                            options.DeliveryEndpoint = "https://localhost:5001/events";
                            options.TemporaryStorageEncryptionKey = SymmetricJwk.FromByteArray(new byte[32]);
                            options.TokenClientOptions.Address = "https://demo.identityserver.io/connect/token";
                            options.TokenClientOptions.ClientId = "m2m";
                            options.TokenClientOptions.ClientSecret = "secret";
                        });
                });
        }
    }
}
