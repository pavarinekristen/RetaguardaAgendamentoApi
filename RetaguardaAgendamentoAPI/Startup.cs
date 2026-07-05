using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RetaguardaAgendamentoAPI.Services.Auth;
using RetaguardaAgendamentoAPI.Services.Email;
using RetaguardaAgendamentoAPI.Services.Sincronizacao;
using System;
using System.Linq;
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

            services.AddControllers();

            var authPermitLimit = Configuration.GetValue<int?>("RateLimiting:Auth:PermitLimit") ?? 20;
            var authWindowSeconds = Configuration.GetValue<int?>("RateLimiting:Auth:WindowSeconds") ?? 60;

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
