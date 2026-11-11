using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Data.Common;
using System.Threading.Tasks;

namespace TaxCommercial.DocGen
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            // Configure Serilog
            
            var basePathLog = String.Empty;

            using var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    if (args.Length > 0)
                    {
                        config.AddJsonFile($"appsettings.{args[0]}.json", true, true);
                    }
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddSerilog();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    #region snippet3
                    var config = hostContext.Configuration;
                    basePathLog = config["BasePathLog"];

                    services.AddSingleton<App>();
                    services.AddSingleton<TaxComService>();
                    services.AddSingleton<CommonService>();

                    services.Configure<TaxComConfig>(config.GetSection(nameof(TaxComConfig)));
                    services.Configure<CLPConfig>(config.GetSection(nameof(CLPConfig)));
                    services.Configure<ClientEmailConfiguration>(config.GetSection(nameof(ClientEmailConfiguration)));
                    services.Configure<UnexpectedEmailConfiguration>(config.GetSection(nameof(UnexpectedEmailConfiguration)));
                    services.AddSingleton<DbConnection>(sp =>
                    {
                        return DbFactory.Create(config.GetConnectionString("TaxCommercialLetters"));
                    });
                    #endregion
                })
                .Build();

            Log.Logger = new LoggerConfiguration()
              .WriteTo.File($"{basePathLog}\\TaxComm.txt", rollingInterval: RollingInterval.Day)
              .CreateLogger();

            await host.StartAsync();

            var app = host.Services.GetRequiredService<App>();



            if (args != null && args.Length > 0)
            {
                await app.RunStatusCheckAsync();            
            }
            else
                await app.RunAsync();

            await host.WaitForShutdownAsync();
        }
    }
}
