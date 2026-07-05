using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RetaguardaAgendamentoAPI.Controllers
{
    [ApiController]
    [Route("health")]
    [Produces("application/json")]
    public class HealthController : ControllerBase
    {
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
            var startedAt = Stopwatch.GetTimestamp();
            var databaseOk = false;
            string databaseMessage;

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(connectionString))
                    throw new InvalidOperationException("ConnectionStrings:DefaultConnection nao configurada.");

                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                await using var command = new MySqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync();

                databaseOk = true;
                databaseMessage = "OK";
                _logger.LogInformation("Health check MySQL OK em {ElapsedMs} ms.", Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, 2));
            }
            catch (Exception ex)
            {
                databaseMessage = ex.Message;
                _logger.LogWarning(ex, "Health check MySQL falhou.");
            }

            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            var response = new
            {
                status = databaseOk ? "OK" : "DEGRADED",
                database = new
                {
                    ok = databaseOk,
                    message = databaseMessage
                },
                elapsedMs = Math.Round(elapsed.TotalMilliseconds, 2),
                checkedAtUtc = DateTime.UtcNow
            };

            return databaseOk ? Ok(response) : StatusCode(503, response);
        }
    }
}
