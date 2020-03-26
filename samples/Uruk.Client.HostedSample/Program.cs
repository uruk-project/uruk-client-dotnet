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
                        .AddSecurityEventTokenClient(o =>
                        {
                            o.EventEndpoint = "https://example.com/events/";
                            o.EncryptionKey = new byte[32];
                        });
                });
        }
    }
}
