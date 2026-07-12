using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RetaguardaAgendamentoAPI.Services
{
    /// <summary>
    /// Verificacao de schema no startup. O schema e criado exclusivamente pelas migrations
    /// (mysql-migrations + apply-mysql-migrations.ps1); a API nao executa mais DDL em runtime
    /// para as tabelas de controle. Se o MySQL estiver acessivel mas o schema estiver
    /// incompleto, a API recusa subir (fail-fast). Se o MySQL estiver indisponivel, a API
    /// sobe mesmo assim e /health reporta DEGRADED, como antes.
    /// </summary>
    public static class SchemaStartupCheck
    {
        private static readonly string[] TabelasRetaguarda =
        {
            "EMPRESA", "RET_USUARIO", "RET_SESSAO", "RET_EMAIL_TOKEN"
        };

        private static readonly string[] TabelasOperacional =
        {
            "AGENDA_SYNC_EXECUCAO", "AGENDA_SYNC_REGISTRO", "AGENDA_DISPOSITIVO",
            "AGENDA_AUDITORIA_OPERACIONAL", "INTEGRACAO_OUTBOX"
        };

        public static async Task VerificarAsync(IConfiguration configuration, ILogger logger)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("ConnectionStrings:DefaultConnection nao configurada.");

            var retaguardaDatabase = new NpgsqlConnectionStringBuilder(connectionString).SearchPath
                ?? "retaguarda_agendamento";
            var operacionalDatabase = Environment.GetEnvironmentVariable("AGENDA_OPERACIONAL_DATABASE")
                ?? configuration.GetValue<string>("AgendaOperacionalDatabase")
                ?? "agenda_operacional";

            var faltantes = new List<string>();

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Postgres indisponivel no startup; verificacao de schema adiada. /health reportara DEGRADED ate o banco voltar.");
                    return;
                }

                faltantes.AddRange(await ListarFaltantesAsync(connection, retaguardaDatabase, TabelasRetaguarda));
            }

            // INFORMATION_SCHEMA so mostra tabelas sobre as quais o usuario tem privilegio;
            // o agenda_operacional pertence ao agenda_admin, entao o check usa a AdminConnection.
            var adminConnectionString = configuration.GetConnectionString("AdminConnection");
            if (string.IsNullOrWhiteSpace(adminConnectionString))
            {
                logger.LogWarning("ConnectionStrings:AdminConnection nao configurada; verificacao do banco operacional pulada.");
            }
            else
            {
                await using var adminConnection = new NpgsqlConnection(adminConnectionString);
                try
                {
                    await adminConnection.OpenAsync();
                    faltantes.AddRange(await ListarFaltantesAsync(adminConnection, operacionalDatabase, TabelasOperacional));
                }
                catch (NpgsqlException ex)
                {
                    logger.LogWarning(ex,
                        "AdminConnection indisponivel no startup; verificacao do schema operacional adiada. Rode .\\apply-postgres-migrations.ps1 se o snapshot falhar.");
                }
            }

            if (faltantes.Count > 0)
            {
                throw new InvalidOperationException(
                    "Schema do Postgres incompleto. Tabelas faltantes: " + string.Join(", ", faltantes) +
                    ". Rode .\\apply-postgres-migrations.ps1 com o Postgres ativo antes de subir a API.");
            }

            logger.LogInformation(
                "Verificacao de schema OK. Retaguarda={RetaguardaDatabase}; Operacional={OperacionalDatabase}",
                retaguardaDatabase,
                operacionalDatabase);
        }

        private static async Task<List<string>> ListarFaltantesAsync(
            NpgsqlConnection connection, string database, IReadOnlyCollection<string> tabelas)
        {
            var existentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await using var command = new NpgsqlCommand(@"
                SELECT TABLE_NAME
                  FROM INFORMATION_SCHEMA.TABLES
                 WHERE TABLE_SCHEMA = @database", connection);
            command.Parameters.AddWithValue("@database", database);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                existentes.Add(reader.GetString(0));

            return tabelas
                .Where(t => !existentes.Contains(t))
                .Select(t => $"{database}.{t}")
                .ToList();
        }
    }
}
