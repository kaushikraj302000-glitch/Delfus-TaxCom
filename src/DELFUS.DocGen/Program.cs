using DELFUS.DocGen.Service;
using DELFUS.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace DELFUS.DocGen
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
              .WriteTo.File("logs\\DELFUS.txt", rollingInterval: RollingInterval.Day)
              .CreateLogger();

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

                    services.AddSingleton<App>();
                    services.AddSingleton<LienService>();
                    services.AddSingleton<CommonService>();

                    services.Configure<DelfusConfig>(config.GetSection(nameof(DelfusConfig)));
                    services.Configure<ClientEmailConfiguration>(config.GetSection(nameof(ClientEmailConfiguration)));
                    services.Configure<UnexpectedEmailConfiguration>(config.GetSection(nameof(UnexpectedEmailConfiguration)));
                    services.AddSingleton<DbConnection>(sp =>
                    {
                        return DbFactory.Create(config.GetConnectionString("DELFUSLetters"));
                    });
                    #endregion
                })
                .Build();

            await host.StartAsync();

            var app = host.Services.GetRequiredService<App>();

            await app.RunAsync();

            await host.WaitForShutdownAsync();
        }
    }
}
