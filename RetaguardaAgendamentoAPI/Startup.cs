using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RetaguardaAgendamentoAPI.Services.Auth;
using RetaguardaAgendamentoAPI.Services.Email;
using RetaguardaAgendamentoAPI.Services.Portal;
using RetaguardaAgendamentoAPI.Services.Sincronizacao;
using System;
using System.Linq;
using System.Net;
using System.Threading.RateLimiting;

namespace RetaguardaAgendamentoAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<EmailService>();
            services.AddScoped<AuthService>();
            services.AddScoped<AgendaSnapshotService>();
            services.AddScoped<PortalClienteService>();

            services.AddMemoryCache();
            services.AddControllers();

            var authPermitLimit = Configuration.GetValue<int?>("RateLimiting:Auth:PermitLimit") ?? 20;
            var authWindowSeconds = Configuration.GetValue<int?>("RateLimiting:Auth:WindowSeconds") ?? 60;
            var healthPermitLimit = Configuration.GetValue<int?>("RateLimiting:Health:PermitLimit") ?? 30;
            var healthWindowSeconds = Configuration.GetValue<int?>("RateLimiting:Health:WindowSeconds") ?? 60;

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = 429;
                options.AddPolicy("AuthPolicy", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = authPermitLimit,
                            Window = TimeSpan.FromSeconds(authWindowSeconds),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
                options.AddPolicy("HealthPolicy", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = healthPermitLimit,
                            Window = TimeSpan.FromSeconds(healthWindowSeconds),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
            });

            services.AddCors(o => o.AddPolicy("RetaguardaAgendamentoPolicy", builder =>
            {
                var allowedOrigins = Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

                if (allowedOrigins.Any(origin => origin == "*"))
                    builder.AllowAnyOrigin();
                else if (allowedOrigins.Length > 0)
                    builder.WithOrigins(allowedOrigins);
                else
                    builder.WithOrigins("http://localhost:5000", "http://127.0.0.1:5000");

                builder.AllowAnyMethod()
                       .AllowAnyHeader();
            }));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            logger.LogInformation("Configurando pipeline HTTP. Environment={EnvironmentName}", env.EnvironmentName);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // TASK API-INFRA 7: atras de reverse proxy (Nginx), o IP real do cliente chega em
            // X-Forwarded-For. Precisa rodar ANTES do rate limiter para as particoes por IP
            // funcionarem. So aceita forwarded headers de proxies explicitamente confiaveis.
            if (Configuration.GetValue<bool?>("ForwardedHeaders:Enabled") ?? false)
            {
                var forwardedOptions = new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                };
                forwardedOptions.KnownProxies.Clear();
                forwardedOptions.KnownNetworks.Clear();

                foreach (var proxy in Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
                {
                    if (IPAddress.TryParse(proxy, out var ip))
                        forwardedOptions.KnownProxies.Add(ip);
                    else
                        logger.LogWarning("ForwardedHeaders:KnownProxies contem valor invalido: {Proxy}", proxy);
                }

                foreach (var network in Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
                {
                    var partes = network.Split('/');
                    if (partes.Length == 2 && IPAddress.TryParse(partes[0], out var prefixo) && int.TryParse(partes[1], out var tamanho))
                        forwardedOptions.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefixo, tamanho));
                    else
                        logger.LogWarning("ForwardedHeaders:KnownNetworks contem CIDR invalido: {Network}", network);
                }

                app.UseForwardedHeaders(forwardedOptions);
                logger.LogInformation(
                    "ForwardedHeaders habilitado. KnownProxies={KnownProxies} KnownNetworks={KnownNetworks}",
                    forwardedOptions.KnownProxies.Count,
                    forwardedOptions.KnownNetworks.Count);
            }

            app.UseRouting();
            app.UseRateLimiter();
            app.UseCors("RetaguardaAgendamentoPolicy");
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
