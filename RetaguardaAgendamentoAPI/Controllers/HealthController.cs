using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RetaguardaAgendamentoAPI.Controllers
{
    [ApiController]
    [Route("health")]
    [Produces("application/json")]
    [EnableRateLimiting("HealthPolicy")]
    public class HealthController : ControllerBase
    {
        // Cache do check de banco (TASK API-INFRA 7): sem ele, cada chamada de /health
        // abre uma conexao nova no Postgres e o endpoint vira vetor barato de carga.
        private static readonly object CacheLock = new();
        private static DateTime _cacheAtUtc = DateTime.MinValue;
        private static bool _cacheOk;
        private static string _cacheMessage = "Nunca verificado";
        private static double _cacheElapsedMs;

        private readonly IConfiguration _configuration;
        private readonly ILogger<HealthController> _logger;

        public HealthController(IConfiguration configuration, ILogger<HealthController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var cacheSeconds = _configuration.GetValue<int?>("Health:DatabaseCacheSeconds") ?? 5;
            bool databaseOk;
            string databaseMessage;
            double elapsedMs;
            DateTime checkedAtUtc;

            lock (CacheLock)
            {
                checkedAtUtc = _cacheAtUtc;
                databaseOk = _cacheOk;
                databaseMessage = _cacheMessage;
                elapsedMs = _cacheElapsedMs;
            }

            if ((DateTime.UtcNow - checkedAtUtc).TotalSeconds >= cacheSeconds)
            {
                var startedAt = Stopwatch.GetTimestamp();
                databaseOk = false;

                try
                {
                    var connectionString = _configuration.GetConnectionString("DefaultConnection");
                    if (string.IsNullOrWhiteSpace(connectionString))
                        throw new InvalidOperationException("ConnectionStrings:DefaultConnection nao configurada.");

                    await using var connection = new NpgsqlConnection(connectionString);
                    await connection.OpenAsync();

                    await using var command = new NpgsqlCommand("SELECT 1", connection);
                    await command.ExecuteScalarAsync();

                    databaseOk = true;
                    databaseMessage = "OK";
                    _logger.LogInformation("Health check Postgres OK em {ElapsedMs} ms.", Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, 2));
                }
                catch (Exception ex)
                {
                    databaseMessage = ex.Message;
                    _logger.LogWarning(ex, "Health check Postgres falhou.");
                }

                elapsedMs = Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, 2);
                checkedAtUtc = DateTime.UtcNow;

                lock (CacheLock)
                {
                    _cacheAtUtc = checkedAtUtc;
                    _cacheOk = databaseOk;
                    _cacheMessage = databaseMessage;
                    _cacheElapsedMs = elapsedMs;
                }
            }

            var response = new
            {
                status = databaseOk ? "OK" : "DEGRADED",
                database = new
                {
                    ok = databaseOk,
                    message = databaseMessage
                },
                elapsedMs,
                checkedAtUtc
            };

            return databaseOk ? Ok(response) : StatusCode(503, response);
        }
    }
}
