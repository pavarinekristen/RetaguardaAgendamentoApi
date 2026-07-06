using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RetaguardaAgendamentoAPI.Services;
using System;

namespace RetaguardaAgendamentoAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            try
            {
                logger.LogInformation("Iniciando RetaguardaAgendamentoAPI.");
                var configuration = host.Services.GetRequiredService<IConfiguration>();
                SchemaStartupCheck.VerificarAsync(configuration, logger).GetAwaiter().GetResult();
                host.Run();
                logger.LogInformation("RetaguardaAgendamentoAPI encerrada.");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "RetaguardaAgendamentoAPI encerrou com erro critico.");
                throw;
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
