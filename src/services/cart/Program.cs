using System;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCoreKit.GrpcTemplate.EfCore;
using NetCoreKit.Infrastructure.EfCore.MySql;
using NetCoreKit.Infrastructure.GrpcHost;
using NetCoreKit.Utils.Extensions;
using VND.CoolStore.Services.Cart.Domain;
using VND.CoolStore.Services.Cart.Infrastructure.Db;
using VND.CoolStore.Services.Cart.Infrastructure.Gateways;
using VND.CoolStore.Services.Cart.v1.Grpc;
using VND.CoolStore.Services.Cart.v1.Services;

namespace VND.CoolStore.Services.Cart
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureDefaultSettings<CartDbContext>(
                    args,
                    svc => { svc.AddEfCoreMySqlDb(); },
                    svc =>
                    {
                        svc.AddHostedService<HostedService>();
                        svc.AddScoped<ICatalogGateway, CatalogGateway>();
                        svc.AddScoped<IPromoGateway, PromoGateway>();
                        svc.AddScoped<IShippingGateway, ShippingGateway>();
                    });

            await host.RunAsync();
        }
    }

    public class HostedService : HostedServiceBase
    {
        private readonly IServiceProvider _resolver;

        public HostedService(IServiceProvider resolver)
            : base(
                resolver.GetRequiredService<ILoggerFactory>(),
                resolver.GetRequiredService<IApplicationLifetime>(),
                resolver.GetRequiredService<IConfiguration>())
        {
            _resolver = resolver;
        }

        protected override Server ConfigureServer()
        {
            var env = _resolver.GetRequiredService<IHostingEnvironment>();
            var host = Config["Hosts:Local:Host"];
            var port = Config["Hosts:Local:Port"].ConvertTo<int>();
            var serviceName = Config["Hosts:ServiceName"];

            if (!env.IsDevelopment())
            {
                port = Environment.GetEnvironmentVariable($"{serviceName.ToUpperInvariant()}_HOST").ConvertTo<int>();
            }

            var server = new Server
            {
                Services =
                {
                    CartService.BindService(new CartServiceImpl(_resolver)),
                    Grpc.Health.V1.Health.BindService(new HealthImpl())
                },
                Ports = { new ServerPort(host, port, ServerCredentials.Insecure) }
            };

            Logger.LogInformation($"{nameof(CartService)} is listening on {host}:{port}.");
            return server;
        }

        protected override void SuppressFinalize()
        {
        }
    }
}
