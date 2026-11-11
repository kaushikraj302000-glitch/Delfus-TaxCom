using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Data.Common;
using System.IO;
using CsvHelper;
using System.Threading.Tasks;
using TPV.DocGen;
using CsvHelper.Configuration;

namespace DELFUS.DocGen
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
              .WriteTo.File("logs\\TPV.txt", rollingInterval: RollingInterval.Day)
              .CreateLogger();

            using var host = Host.CreateDefaultBuilder(args)
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
                    services.AddSingleton<TPVService>();
                    services.Configure<TPVConfig>(config.GetSection(nameof(TPVConfig)));
                    services.AddSingleton<DbConnection>(sp =>
                    {
                        return DbFactory.Create(config.GetConnectionString("TPVLetters"));
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
