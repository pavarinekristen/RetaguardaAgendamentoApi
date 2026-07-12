using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RetaguardaAgendamentoAPI.Models.Sincronizacao;

namespace RetaguardaAgendamentoAPI.Services.Sincronizacao
{
    public class AgendaSnapshotService
    {
        private readonly string _adminConnectionString;
        private readonly string _retaguardaDatabase;
        private readonly string _operacionalDatabaseOverride;
        private readonly bool _marcarAusentesComoExcluidos;
        private readonly ILogger<AgendaSnapshotService> _logger;

        private static readonly HashSet<string> TabelasIgnoradas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "__EFMIGRATIONSHISTORY",
            "SQLITE_SEQUENCE"
        };

        private static readonly Dictionary<string, string> EmpresaColunas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RazaoSocial"] = "RAZAO_SOCIAL",
            ["NomeFantasia"] = "NOME_FANTASIA",
            ["Cnpj"] = "CNPJ",
            ["InscricaoEstadual"] = "INSCRICAO_ESTADUAL",
            ["InscricaoMunicipal"] = "INSCRICAO_MUNICIPAL",
            ["TipoRegime"] = "TIPO_REGIME",
            ["Crt"] = "CRT",
            ["DataConstituicao"] = "DATA_CONSTITUICAO",
            ["Tipo"] = "TIPO",
            ["Email"] = "EMAIL",
            ["Logradouro"] = "LOGRADOURO",
            ["Numero"] = "NUMERO",
            ["Complemento"] = "COMPLEMENTO",
            ["Cep"] = "CEP",
            ["Bairro"] = "BAIRRO",
            ["Cidade"] = "CIDADE",
            ["Uf"] = "UF",
            ["Fone"] = "FONE",
            ["Contato"] = "CONTATO",
            ["CodigoIbgeCidade"] = "CODIGO_IBGE_CIDADE",
            ["CodigoIbgeUf"] = "CODIGO_IBGE_UF",
            ["Logotipo"] = "LOGOTIPO",
            ["Registrado"] = "REGISTRADO",
            ["NaturezaJuridica"] = "NATUREZA_JURIDICA",
            ["Simei"] = "SIMEI",
            ["EmailPagamento"] = "EMAIL_PAGAMENTO",
            ["DataRegistro"] = "DATA_REGISTRO",
            ["HoraRegistro"] = "HORA_REGISTRO"
        };

        private sealed class RegistroFinalPreparado
        {
            public string IdLocal { get; set; }
            public string Hash { get; set; }
            public string DadosJson { get; set; }
            public Dictionary<string, object> Dados { get; set; }
        }

        public AgendaSnapshotService(IConfiguration configuration, ILogger<AgendaSnapshotService> logger)
        {
            _adminConnectionString = configuration.GetConnectionString("AdminConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:AdminConnection nao configurada.");
            _logger = logger;

            _retaguardaDatabase = ObterDatabase(
                configuration.GetConnectionString("DefaultConnection"))
                ?? "retaguarda_agendamento";

            _operacionalDatabaseOverride = Environment.GetEnvironmentVariable("AGENDA_OPERACIONAL_DATABASE")
                ?? configuration.GetValue<string>("AgendaOperacionalDatabase");

            // Padrao conservador: snapshot incompleto nao deve esconder dados antigos.
            // Para ativar soft-exclusao, configure Sincronizacao:MarcarAusentesComoExcluidos=true.
            _marcarAusentesComoExcluidos = configuration.GetValue<bool?>("Sincronizacao:MarcarAusentesComoExcluidos") ?? false;
        }

        // Identificador dinamico -> minusculo e com aspas (protege palavras reservadas e casa
        // com o schema em minusculo). Usado para tabelas/colunas cujo nome vem do snapshot.
        private static string Ident(string nome) => "\"" + (nome ?? string.Empty).ToLowerInvariant() + "\"";

        // Nome de indice/constraint gerado a partir do nome da tabela (apenas [A-Z0-9_]).
        private static string NomeObjeto(string nome) => (nome ?? string.Empty).ToLowerInvariant();

        public async Task<AgendaSnapshotResponse> SalvarSnapshotAsync(int empresaId, string cnpj, AgendaSnapshotRequest request)
        {
            if (request == null)
                throw new ArgumentException("Snapshot nao informado.");

            if (empresaId <= 0)
                throw new ArgumentException("Empresa invalida para sincronizacao.");

            cnpj = NormalizarCnpj(cnpj);
            if (!CnpjNormalizadoValido(cnpj))
                throw new ArgumentException("CNPJ invalido para sincronizacao.");

            var operacionalDatabase = NomeBancoOperacional();
            var dispositivoId = ValorOuPadrao(request.DispositivoId, "AGENDA-WPF");
            var tabelas = request.Tabelas
                .Where(t => !string.IsNullOrWhiteSpace(t.Nome))
                .Select(t => new AgendaSnapshotTable
                {
                    Nome = NormalizarNomeTabela(t.Nome),
                    Registros = t.Registros ?? new List<AgendaSnapshotRecord>()
                })
                .Where(t => !TabelasIgnoradas.Contains(t.Nome))
                .ToList();

            var agora = DateTime.UtcNow;

            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            _logger.LogInformation("Conexao Postgres administrativa aberta para salvar snapshot. BancoOperacional={BancoOperacional}", operacionalDatabase);

            // As tabelas de controle sao criadas pelas migrations (postgres-migrations/001_baseline_schema.sql).
            // Aqui apenas apontamos o search_path para o schema operacional.
            await ExecutarAsync(connection, $"SET search_path TO {operacionalDatabase}");
            await GarantirTabelasFinaisAsync(connection, tabelas);

            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                var execucaoId = await RegistrarExecucaoAsync(
                    connection,
                    (NpgsqlTransaction)transaction,
                    empresaId,
                    cnpj,
                    dispositivoId,
                    tabelas,
                    agora);

                foreach (var tabela in tabelas)
                {
                    var registrosPreparados = PrepararRegistrosFinais(tabela);
                    var idsAtivos = registrosPreparados
                        .Select(r => r.IdLocal)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    await RegistrarSnapshotsBatchAsync(
                        connection,
                        (NpgsqlTransaction)transaction,
                        execucaoId,
                        empresaId,
                        tabela.Nome,
                        dispositivoId,
                        registrosPreparados,
                        agora);

                    await AplicarRegistrosFinaisBatchAsync(
                        connection,
                        (NpgsqlTransaction)transaction,
                        empresaId,
                        dispositivoId,
                        tabela.Nome,
                        registrosPreparados,
                        agora);

                    await RegistrarAuditoriaBatchAsync(
                        connection,
                        (NpgsqlTransaction)transaction,
                        empresaId,
                        dispositivoId,
                        tabela.Nome,
                        registrosPreparados,
                        agora);

                    await RegistrarOutboxProcessadaBatchAsync(
                        connection,
                        (NpgsqlTransaction)transaction,
                        empresaId,
                        tabela.Nome,
                        registrosPreparados,
                        agora);

                    if (tabela.Nome.Equals("EMPRESA", StringComparison.OrdinalIgnoreCase))
                        foreach (var registro in registrosPreparados)
                            await AplicarEmpresaAdministrativaAsync(
                                connection,
                                (NpgsqlTransaction)transaction,
                                empresaId,
                                tabela.Nome,
                                registro.Dados);

                    if (_marcarAusentesComoExcluidos)
                    {
                        await MarcarAusentesComoExcluidosAsync(
                            connection,
                            (NpgsqlTransaction)transaction,
                            empresaId,
                            dispositivoId,
                            tabela.Nome,
                            idsAtivos,
                            agora);
                    }
                }

                await RegistrarDispositivoAsync(
                    connection,
                    (NpgsqlTransaction)transaction,
                    empresaId,
                    dispositivoId,
                    agora);

                await using var finalizar = new NpgsqlCommand(
                    "UPDATE agenda_sync_execucao SET FINALIZADO_EM = (now() at time zone 'utc') WHERE ID = @id",
                    connection,
                    (NpgsqlTransaction)transaction);
                finalizar.Parameters.AddWithValue("@id", execucaoId);
                await finalizar.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                _logger.LogError(
                    "Rollback do snapshot executado. EmpresaId={EmpresaId}; BancoOperacional={BancoOperacional}; DispositivoId={DispositivoId}",
                    empresaId,
                    operacionalDatabase,
                    dispositivoId);
                throw;
            }

            return new AgendaSnapshotResponse
            {
                Cnpj = cnpj,
                BancoOperacional = operacionalDatabase,
                DispositivoId = dispositivoId,
                TotalTabelas = tabelas.Count,
                TotalRegistros = tabelas.Sum(t => t.Registros.Count),
                SincronizadoEm = agora
            };
        }

        private static async Task<long> RegistrarExecucaoAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            int empresaId,
            string cnpj,
            string dispositivoId,
            IReadOnlyCollection<AgendaSnapshotTable> tabelas,
            DateTime agora)
        {
            await using var exec = new NpgsqlCommand(@"
                INSERT INTO agenda_sync_execucao
                    (ID_EMPRESA, CNPJ, DISPOSITIVO_ID, INICIADO_EM, TOTAL_TABELAS, TOTAL_REGISTROS)
                VALUES
                    (@empresaId, @cnpj, @dispositivo, @agora, @tabelas, @registros)
                RETURNING ID;", connection, transaction);
            exec.Parameters.AddWithValue("@empresaId", empresaId);
            exec.Parameters.AddWithValue("@cnpj", cnpj);
            exec.Parameters.AddWithValue("@dispositivo", dispositivoId);
            exec.Parameters.AddWithValue("@agora", agora);
            exec.Parameters.AddWithValue("@tabelas", tabelas.Count);
            exec.Parameters.AddWithValue("@registros", tabelas.Sum(t => t.Registros.Count));
            return Convert.ToInt64(await exec.ExecuteScalarAsync());
        }

        private static async Task RegistrarSnapshotsBatchAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            long execucaoId,
            int empresaId,
            string tabela,
            string dispositivoId,
            IReadOnlyList<RegistroFinalPreparado> registros,
            DateTime agora)
        {
            const int chunkSize = 500;
            foreach (var chunk in Chunk(registros, chunkSize))
            {
                var valores = new List<string>(chunk.Count);
                await using var insert = new NpgsqlCommand { Connection = connection, Transaction = transaction };

                for (var i = 0; i < chunk.Count; i++)
                {
                    var prefix = i * 8;
                    valores.Add($"(@p{prefix}, @p{prefix + 1}, @p{prefix + 2}, @p{prefix + 3}, @p{prefix + 4}, @p{prefix + 5}, @p{prefix + 6}, @p{prefix + 7})");
                    insert.Parameters.AddWithValue($"@p{prefix}", execucaoId);
                    insert.Parameters.AddWithValue($"@p{prefix + 1}", empresaId);
                    insert.Parameters.AddWithValue($"@p{prefix + 2}", tabela);
                    insert.Parameters.AddWithValue($"@p{prefix + 3}", chunk[i].IdLocal);
                    insert.Parameters.AddWithValue($"@p{prefix + 4}", dispositivoId);
                    insert.Parameters.AddWithValue($"@p{prefix + 5}", chunk[i].DadosJson);
                    insert.Parameters.AddWithValue($"@p{prefix + 6}", chunk[i].Hash);
                    insert.Parameters.AddWithValue($"@p{prefix + 7}", agora);
                }

                insert.CommandText = $@"
                INSERT INTO agenda_sync_registro
                    (ID_EXECUCAO, ID_EMPRESA, TABELA, ID_LOCAL, DISPOSITIVO_ID, DADOS_JSON, HASH_SHA256, SINCRONIZADO_EM)
                VALUES
                    {string.Join(", ", valores)}
                ON CONFLICT (id_empresa, tabela, id_local, dispositivo_id) DO UPDATE SET
                    ID_EXECUCAO = EXCLUDED.ID_EXECUCAO,
                    DADOS_JSON = EXCLUDED.DADOS_JSON,
                    HASH_SHA256 = EXCLUDED.HASH_SHA256,
                    SINCRONIZADO_EM = EXCLUDED.SINCRONIZADO_EM";
                await insert.ExecuteNonQueryAsync();
            }
        }

        private static async Task GarantirTabelasFinaisAsync(NpgsqlConnection connection, IEnumerable<AgendaSnapshotTable> tabelas)
        {
            var database = await ObterDatabaseAtualAsync(connection);
            foreach (var tabela in tabelas)
            {
                if (tabela.Nome.StartsWith("AGENDA_SYNC_", StringComparison.OrdinalIgnoreCase))
                    continue;

                var colunas = ExtrairColunas(tabela)
                    .Select(NormalizarNomeColuna)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Where(c => !EhColunaReservada(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var ukName = NomeObjeto($"UK_{tabela.Nome}_TENANT_LOCAL_DEVICE");
                var ixName = NomeObjeto($"IX_{tabela.Nome}_EMPRESA");

                if (!await TabelaExisteAsync(connection, database, tabela.Nome))
                {
                    var colunasPayloadSql = colunas.Count == 0
                        ? string.Empty
                        : string.Join(Environment.NewLine, colunas.Select(c => $"                        {Ident(c)} TEXT,"));

                    await ExecutarAsync(connection, $@"
                    CREATE TABLE {Ident(tabela.Nome)} (
                        ""id"" BIGSERIAL PRIMARY KEY,
                        ""id_empresa"" INTEGER NOT NULL,
                        ""id_local"" VARCHAR(128) NOT NULL,
                        ""dispositivo_id"" VARCHAR(64) NOT NULL,
                        ""sincronizado_em"" TIMESTAMP NOT NULL,
                        ""hash_sha256"" VARCHAR(64),
                        ""dados_json"" TEXT,
                        ""excluido"" CHAR(1) NOT NULL DEFAULT 'N',
{colunasPayloadSql}
                        CONSTRAINT {ukName} UNIQUE (""id_empresa"", ""id_local"", ""dispositivo_id"")
                    )");

                    await ExecutarAsync(connection, $"CREATE INDEX {ixName} ON {Ident(tabela.Nome)} (\"id_empresa\")");
                }
                else
                {
                    await GarantirColunaAsync(connection, tabela.Nome, "ID_EMPRESA", "INTEGER NOT NULL DEFAULT 0");
                    await GarantirColunaAsync(connection, tabela.Nome, "DISPOSITIVO_ID", "VARCHAR(64) NOT NULL DEFAULT 'AGENDA-WPF'");
                    await GarantirColunaAsync(connection, tabela.Nome, "HASH_SHA256", "VARCHAR(64)");
                    await GarantirColunaAsync(connection, tabela.Nome, "DADOS_JSON", "TEXT");
                    await GarantirColunaAsync(connection, tabela.Nome, "EXCLUIDO", "CHAR(1) NOT NULL DEFAULT 'N'");
                    await GarantirIndiceAsync(connection, ukName, $"CREATE UNIQUE INDEX {ukName} ON {Ident(tabela.Nome)} (\"id_empresa\", \"id_local\", \"dispositivo_id\")");
                    await GarantirIndiceAsync(connection, ixName, $"CREATE INDEX {ixName} ON {Ident(tabela.Nome)} (\"id_empresa\")");

                    foreach (var coluna in colunas)
                        await GarantirColunaAsync(connection, tabela.Nome, coluna, "TEXT");
                }

                await GarantirIndicesOperacionaisAsync(connection, tabela.Nome, colunas);
            }
        }

        private static async Task AplicarRegistrosFinaisBatchAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            int empresaId,
            string dispositivoId,
            string nomeTabela,
            IReadOnlyList<RegistroFinalPreparado> registros,
            DateTime agora)
        {
            if (nomeTabela.StartsWith("AGENDA_SYNC_", StringComparison.OrdinalIgnoreCase))
                return;

            if (registros.Count == 0)
                return;

            var colunas = registros
                .SelectMany(r => r.Dados.Keys)
                .Select(NormalizarNomeColuna)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Where(c => !EhColunaReservada(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var nomesColunas = new List<string>
            {
                "ID_EMPRESA",
                "ID_LOCAL",
                "DISPOSITIVO_ID",
                "SINCRONIZADO_EM",
                "HASH_SHA256",
                "DADOS_JSON",
                "EXCLUIDO"
            };
            nomesColunas.AddRange(colunas);

            var atualizacoes = nomesColunas
                .Where(c => !c.Equals("ID_EMPRESA", StringComparison.OrdinalIgnoreCase))
                .Where(c => !c.Equals("ID_LOCAL", StringComparison.OrdinalIgnoreCase))
                .Where(c => !c.Equals("DISPOSITIVO_ID", StringComparison.OrdinalIgnoreCase))
                .Select(c => $"{Ident(c)} = EXCLUDED.{Ident(c)}");

            var chunkSize = Math.Max(25, Math.Min(300, 60000 / Math.Max(1, nomesColunas.Count)));
            foreach (var chunk in Chunk(registros, chunkSize))
            {
                var values = new List<string>(chunk.Count);
                await using var command = new NpgsqlCommand { Connection = connection, Transaction = transaction };

                for (var row = 0; row < chunk.Count; row++)
                {
                    var parametros = new List<string>(nomesColunas.Count);
                    for (var col = 0; col < nomesColunas.Count; col++)
                    {
                        var paramName = $"@p{row}_{col}";
                        parametros.Add(paramName);
                        command.Parameters.AddWithValue(paramName, ValorColunaFinal(nomesColunas[col], chunk[row], empresaId, dispositivoId, agora));
                    }

                    values.Add($"({string.Join(", ", parametros)})");
                }

                command.CommandText = $@"
                    INSERT INTO {Ident(nomeTabela)}
                        ({string.Join(", ", nomesColunas.Select(Ident))})
                    VALUES
                        {string.Join(", ", values)}
                    ON CONFLICT (""id_empresa"", ""id_local"", ""dispositivo_id"") DO UPDATE SET
                        {string.Join(", ", atualizacoes)}";

                await command.ExecuteNonQueryAsync();
            }
        }

        private static async Task MarcarAusentesComoExcluidosAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            int empresaId,
            string dispositivoId,
            string nomeTabela,
            HashSet<string> idsAtivos,
            DateTime agora)
        {
            if (nomeTabela.StartsWith("AGENDA_SYNC_", StringComparison.OrdinalIgnoreCase))
                return;

            var parametros = idsAtivos.Select((_, index) => $"@id{index}").ToList();
            var filtroIds = idsAtivos.Count == 0
                ? string.Empty
                : $"AND ID_LOCAL NOT IN ({string.Join(", ", parametros)})";

            var sql = $@"
                UPDATE {Ident(nomeTabela)}
                   SET EXCLUIDO = 'S',
                       SINCRONIZADO_EM = @agora
                 WHERE ID_EMPRESA = @empresaId
                   AND DISPOSITIVO_ID = @dispositivoId
                   {filtroIds}";

            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@agora", agora);
            command.Parameters.AddWithValue("@empresaId", empresaId);
            command.Parameters.AddWithValue("@dispositivoId", dispositivoId);

            var i = 0;
            foreach (var id in idsAtivos)
                command.Parameters.AddWithValue($"@id{i++}", id);

            await command.ExecuteNonQueryAsync();
        }

        private async Task AplicarEmpresaAdministrativaAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            int empresaId,
            string nomeTabela,
            Dictionary<string, object> dados)
        {
            if (!nomeTabela.Equals("EMPRESA", StringComparison.OrdinalIgnoreCase))
                return;

            if (!await TabelaExisteAsync(connection, _retaguardaDatabase, "EMPRESA"))
                return;

            var colunasExistentes = await ListarColunasAsync(connection, _retaguardaDatabase, "EMPRESA");
            var pares = EmpresaColunas
                .Where(p => dados.ContainsKey(p.Key))
                .Where(p => colunasExistentes.Contains(p.Value))
                .Where(p => !p.Value.Equals("CNPJ", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (pares.Count == 0)
                return;

            var set = string.Join(", ", pares.Select((p, index) => $"{Ident(p.Value)} = @p{index}"));
            await using var update = new NpgsqlCommand(
                $"UPDATE {_retaguardaDatabase}.empresa SET {set} WHERE ID = @id",
                connection,
                transaction);

            for (var i = 0; i < pares.Count; i++)
                update.Parameters.AddWithValue($"@p{i}", ConverterValorFinal(pares[i].Key, dados[pares[i].Key]));

            update.Parameters.AddWithValue("@id", empresaId);
            await update.ExecuteNonQueryAsync();
        }

        private static async Task RegistrarAuditoriaBatchAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            int empresaId,
            string dispositivoId,
            string tabela,
            IReadOnlyList<RegistroFinalPreparado> registros,
            DateTime agora)
        {
            if (registros.Count == 0)
                return;

            const int chunkSize = 500;
            foreach (var chunk in Chunk(registros, chunkSize))
            {
                var valores = new List<string>(chunk.Count);
                await using var command = new NpgsqlCommand { Connection = connection, Transaction = transaction };

                for (var i = 0; i < chunk.Count; i++)
                {
                    var prefix = i * 7;
                    valores.Add($"(@p{prefix}, @p{prefix + 1}, @p{prefix + 2}, @p{prefix + 3}, @p{prefix + 4}, @p{prefix + 5}, @p{prefix + 6})");
                    command.Parameters.AddWithValue($"@p{prefix}", empresaId);
                    command.Parameters.AddWithValue($"@p{prefix + 1}", tabela);
                    command.Parameters.AddWithValue($"@p{prefix + 2}", chunk[i].IdLocal);
                    command.Parameters.AddWithValue($"@p{prefix + 3}", dispositivoId);
                    command.Parameters.AddWithValue($"@p{prefix + 4}", "UPSERT");
                    command.Parameters.AddWithValue($"@p{prefix + 5}", chunk[i].DadosJson);
                    command.Parameters.AddWithValue($"@p{prefix + 6}", agora);
                }

                command.CommandText = $@"
                INSERT INTO agenda_auditoria_operacional
                    (ID_EMPRESA, TABELA, ID_LOCAL, DISPOSITIVO_ID, ACAO, DADOS_JSON, CRIADO_EM)
                VALUES
                    {string.Join(", ", valores)}";
                await command.ExecuteNonQueryAsync();
            }
        }

        private static async Task RegistrarOutboxProcessadaBatchAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            int empresaId,
            string tabela,
            IReadOnlyList<RegistroFinalPreparado> registros,
            DateTime agora)
        {
            if (registros.Count == 0)
                return;

            const int chunkSize = 500;
            foreach (var chunk in Chunk(registros, chunkSize))
            {
                var valores = new List<string>(chunk.Count);
                await using var command = new NpgsqlCommand { Connection = connection, Transaction = transaction };

                for (var i = 0; i < chunk.Count; i++)
                {
                    var prefix = i * 6;
                    valores.Add($"(@p{prefix}, 'AGENDA', @p{prefix + 1}, @p{prefix + 2}, @p{prefix + 3}, 'PROCESSADO', @p{prefix + 4}, @p{prefix + 5})");
                    command.Parameters.AddWithValue($"@p{prefix}", empresaId);
                    command.Parameters.AddWithValue($"@p{prefix + 1}", tabela);
                    command.Parameters.AddWithValue($"@p{prefix + 2}", chunk[i].IdLocal);
                    command.Parameters.AddWithValue($"@p{prefix + 3}", chunk[i].DadosJson);
                    command.Parameters.AddWithValue($"@p{prefix + 4}", agora);
                    command.Parameters.AddWithValue($"@p{prefix + 5}", agora);
                }

                command.CommandText = $@"
                INSERT INTO integracao_outbox
                    (ID_EMPRESA, ORIGEM, ENTIDADE, ID_LOCAL, PAYLOAD_JSON, STATUS, CRIADO_EM, PROCESSADO_EM)
                VALUES
                    {string.Join(", ", valores)}";
                await command.ExecuteNonQueryAsync();
            }
        }

        private static async Task RegistrarDispositivoAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            int empresaId,
            string dispositivoId,
            DateTime agora)
        {
            await using var command = new NpgsqlCommand(@"
                INSERT INTO agenda_dispositivo
                    (ID_EMPRESA, DISPOSITIVO_ID, NOME, ULTIMA_SINCRONIZACAO_EM, ATIVO)
                VALUES
                    (@empresaId, @dispositivoId, @dispositivoId, @agora, 'S')
                ON CONFLICT (id_empresa, dispositivo_id) DO UPDATE SET
                    ULTIMA_SINCRONIZACAO_EM = EXCLUDED.ULTIMA_SINCRONIZACAO_EM,
                    ATIVO = 'S'", connection, transaction);
            command.Parameters.AddWithValue("@empresaId", empresaId);
            command.Parameters.AddWithValue("@dispositivoId", dispositivoId);
            command.Parameters.AddWithValue("@agora", agora);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<bool> TabelaExisteAsync(NpgsqlConnection connection, string database, string tabela)
        {
            await using var command = new NpgsqlCommand(@"
                SELECT COUNT(*)
                  FROM INFORMATION_SCHEMA.TABLES
                 WHERE TABLE_SCHEMA = @database
                   AND LOWER(TABLE_NAME) = LOWER(@tabela)", connection);
            command.Parameters.AddWithValue("@database", database);
            command.Parameters.AddWithValue("@tabela", tabela);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
        }

        private static async Task<string> ObterDatabaseAtualAsync(NpgsqlConnection connection)
        {
            await using var command = new NpgsqlCommand("SELECT current_schema()", connection);
            var database = Convert.ToString(await command.ExecuteScalarAsync());
            if (string.IsNullOrWhiteSpace(database))
                throw new InvalidOperationException("Nenhum schema selecionado para sincronizacao da agenda.");

            return database;
        }

        private static async Task<HashSet<string>> ListarColunasAsync(NpgsqlConnection connection, string database, string tabela)
        {
            var colunas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var command = new NpgsqlCommand(@"
                SELECT COLUMN_NAME
                  FROM INFORMATION_SCHEMA.COLUMNS
                 WHERE TABLE_SCHEMA = @database
                   AND LOWER(TABLE_NAME) = LOWER(@tabela)", connection);
            command.Parameters.AddWithValue("@database", database);
            command.Parameters.AddWithValue("@tabela", tabela);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                colunas.Add(reader.GetString(0));

            return colunas;
        }

        private static async Task GarantirColunaAsync(NpgsqlConnection connection, string tabela, string coluna, string definicao)
        {
            if (!await ColunaExisteAsync(connection, tabela, coluna))
                await ExecutarAsync(connection, $"ALTER TABLE {Ident(tabela)} ADD COLUMN {Ident(coluna)} {definicao}");
        }

        private static async Task<bool> ColunaExisteAsync(NpgsqlConnection connection, string tabela, string coluna)
        {
            await using var command = new NpgsqlCommand(@"
                SELECT COUNT(*)
                  FROM INFORMATION_SCHEMA.COLUMNS
                 WHERE TABLE_SCHEMA = current_schema()
                   AND LOWER(TABLE_NAME) = LOWER(@tabela)
                   AND LOWER(COLUMN_NAME) = LOWER(@coluna)", connection);
            command.Parameters.AddWithValue("@tabela", tabela);
            command.Parameters.AddWithValue("@coluna", coluna);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
        }

        private static async Task GarantirIndiceAsync(NpgsqlConnection connection, string indice, string createSql)
        {
            await using var command = new NpgsqlCommand(@"
                SELECT COUNT(*)
                  FROM pg_indexes
                 WHERE schemaname = current_schema()
                   AND LOWER(indexname) = LOWER(@indice)", connection);
            command.Parameters.AddWithValue("@indice", indice);

            if (Convert.ToInt32(await command.ExecuteScalarAsync()) == 0)
                await ExecutarAsync(connection, createSql);
        }

        private static async Task GarantirIndicesOperacionaisAsync(NpgsqlConnection connection, string tabela, IReadOnlyCollection<string> colunas)
        {
            if (tabela.StartsWith("AGENDA_SYNC_", StringComparison.OrdinalIgnoreCase))
                return;

            await GarantirIndiceAsync(
                connection,
                NomeObjeto($"IX_{tabela}_EMPRESA_DEVICE_LOCAL"),
                $"CREATE INDEX {NomeObjeto($"IX_{tabela}_EMPRESA_DEVICE_LOCAL")} ON {Ident(tabela)} (\"id_empresa\", \"dispositivo_id\", \"id_local\")");

            await GarantirIndiceAsync(
                connection,
                NomeObjeto($"IX_{tabela}_EMPRESA_ATIVOS_SYNC"),
                $"CREATE INDEX {NomeObjeto($"IX_{tabela}_EMPRESA_ATIVOS_SYNC")} ON {Ident(tabela)} (\"id_empresa\", \"sincronizado_em\" DESC) WHERE \"excluido\" <> 'S'");

            if (colunas.Contains("NOME", StringComparer.OrdinalIgnoreCase))
                await GarantirIndiceAsync(
                    connection,
                    NomeObjeto($"IX_{tabela}_EMPRESA_NOME_ATIVOS"),
                    $"CREATE INDEX {NomeObjeto($"IX_{tabela}_EMPRESA_NOME_ATIVOS")} ON {Ident(tabela)} (\"id_empresa\", \"nome\") WHERE \"excluido\" <> 'S'");

            if (!await ExtensaoExisteAsync(connection, "pg_trgm"))
                return;

            foreach (var coluna in new[] { "NOME", "EMPRESA", "ID_TEXTO", "CODIGO" })
            {
                if (!colunas.Contains(coluna, StringComparer.OrdinalIgnoreCase))
                    continue;

                var indice = NomeObjeto($"IX_{tabela}_{coluna}_TRGM");
                await GarantirIndiceAsync(
                    connection,
                    indice,
                    $"CREATE INDEX {indice} ON {Ident(tabela)} USING gin (lower(coalesce({Ident(coluna)}, '')) gin_trgm_ops)");
            }
        }

        private static async Task<bool> ExtensaoExisteAsync(NpgsqlConnection connection, string nome)
        {
            await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM pg_extension WHERE extname = @nome", connection);
            command.Parameters.AddWithValue("@nome", nome);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
        }

        private static IReadOnlyList<string> ExtrairColunas(AgendaSnapshotTable tabela)
        {
            var colunas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var registro in tabela.Registros)
            {
                foreach (var coluna in LerDados(registro.DadosJson).Keys)
                    colunas.Add(coluna);
            }

            return colunas.ToList();
        }

        private static Dictionary<string, object> LerDados(string dadosJson)
        {
            var dados = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(dadosJson))
                return dados;

            using var document = JsonDocument.Parse(dadosJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return dados;

            foreach (var property in document.RootElement.EnumerateObject())
                dados[property.Name] = ConverterValorJson(property.Value);

            return dados;
        }

        private static IReadOnlyList<RegistroFinalPreparado> PrepararRegistrosFinais(AgendaSnapshotTable tabela)
        {
            var registros = new List<RegistroFinalPreparado>(tabela.Registros.Count);
            foreach (var registro in tabela.Registros)
            {
                registros.Add(new RegistroFinalPreparado
                {
                    IdLocal = ValorOuPadrao(registro.IdLocal, registro.Hash, Guid.NewGuid().ToString("N")),
                    Hash = ValorOuPadrao(registro.Hash, string.Empty),
                    DadosJson = registro.DadosJson ?? "{}",
                    Dados = LerDados(registro.DadosJson)
                });
            }

            return registros;
        }

        private static object ValorColunaFinal(
            string coluna,
            RegistroFinalPreparado registro,
            int empresaId,
            string dispositivoId,
            DateTime agora)
        {
            if (coluna.Equals("ID_EMPRESA", StringComparison.OrdinalIgnoreCase))
                return empresaId;
            if (coluna.Equals("ID_LOCAL", StringComparison.OrdinalIgnoreCase))
                return registro.IdLocal;
            if (coluna.Equals("DISPOSITIVO_ID", StringComparison.OrdinalIgnoreCase))
                return dispositivoId;
            if (coluna.Equals("SINCRONIZADO_EM", StringComparison.OrdinalIgnoreCase))
                return agora;
            if (coluna.Equals("HASH_SHA256", StringComparison.OrdinalIgnoreCase))
                return registro.Hash;
            if (coluna.Equals("DADOS_JSON", StringComparison.OrdinalIgnoreCase))
                return registro.DadosJson;
            if (coluna.Equals("EXCLUIDO", StringComparison.OrdinalIgnoreCase))
                return "N";

            var chave = registro.Dados.Keys.FirstOrDefault(k => NormalizarNomeColuna(k).Equals(coluna, StringComparison.OrdinalIgnoreCase));
            return chave == null ? DBNull.Value : ConverterValorFinal(chave, registro.Dados[chave]);
        }

        private static object ConverterValorJson(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.Null => DBNull.Value,
                JsonValueKind.Undefined => DBNull.Value,
                JsonValueKind.True => "S",
                JsonValueKind.False => "N",
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.String => value.GetString() ?? string.Empty,
                _ => value.GetRawText()
            };
        }

        private static object ConverterValorFinal(string propriedade, object valor)
        {
            if (valor == null || valor == DBNull.Value)
                return DBNull.Value;

            if (propriedade.EndsWith("Cnpj", StringComparison.OrdinalIgnoreCase) ||
                propriedade.Equals("CpfCnpj", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizarCnpj(valor.ToString() ?? string.Empty);
            }

            if (propriedade.Equals("Cpf", StringComparison.OrdinalIgnoreCase) ||
                propriedade.Equals("Cep", StringComparison.OrdinalIgnoreCase))
            {
                return ApenasDigitos(valor.ToString() ?? string.Empty);
            }

            return valor;
        }

        private static string ObterDatabase(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return null;

            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return string.IsNullOrWhiteSpace(builder.SearchPath) ? null : builder.SearchPath;
        }

        private string NomeBancoOperacional()
        {
            if (!string.IsNullOrWhiteSpace(_operacionalDatabaseOverride))
                return NormalizarNomeDatabase(_operacionalDatabaseOverride);

            return "agenda_operacional";
        }

        private static string NormalizarNomeDatabase(string nome)
        {
            var normalizado = Regex.Replace((nome ?? string.Empty).Trim().ToLowerInvariant(), "[^a-z0-9_]", "_");
            normalizado = Regex.Replace(normalizado, "_+", "_").Trim('_');
            return string.IsNullOrWhiteSpace(normalizado) ? "agenda_operacional" : normalizado;
        }

        private static async Task ExecutarAsync(NpgsqlConnection connection, string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private static string ApenasDigitos(string valor)
        {
            return new string((valor ?? string.Empty).Where(char.IsDigit).ToArray());
        }

        private static string NormalizarCnpj(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return string.Empty;

            var caracteres = new List<char>();
            foreach (var c in valor)
            {
                if (char.IsDigit(c))
                {
                    caracteres.Add(c);
                    continue;
                }

                var letra = char.ToUpperInvariant(c);
                if (letra >= 'A' && letra <= 'Z')
                    caracteres.Add(letra);
            }

            return new string(caracteres.ToArray());
        }

        private static bool CnpjNormalizadoValido(string cnpj)
        {
            return cnpj.Length == 14 && cnpj.All(c => char.IsDigit(c) || (c >= 'A' && c <= 'Z'));
        }

        private static string NormalizarNomeTabela(string nome)
        {
            var normalizado = Regex.Replace((nome ?? string.Empty).Trim().ToUpperInvariant(), "[^A-Z0-9_]", "_");
            return string.IsNullOrWhiteSpace(normalizado) ? "TABELA_SEM_NOME" : normalizado;
        }

        private static string NormalizarNomeColuna(string nome)
        {
            var comUnderscore = Regex.Replace((nome ?? string.Empty).Trim(), "([a-z0-9])([A-Z])", "$1_$2");
            var normalizado = Regex.Replace(comUnderscore.ToUpperInvariant(), "[^A-Z0-9_]", "_");
            normalizado = Regex.Replace(normalizado, "_+", "_").Trim('_');
            return string.IsNullOrWhiteSpace(normalizado) ? string.Empty : normalizado;
        }

        private static bool EhColunaReservada(string coluna)
        {
            return coluna.Equals("ID", StringComparison.OrdinalIgnoreCase)
                || coluna.Equals("ID_EMPRESA", StringComparison.OrdinalIgnoreCase)
                || coluna.Equals("ID_LOCAL", StringComparison.OrdinalIgnoreCase)
                || coluna.Equals("DISPOSITIVO_ID", StringComparison.OrdinalIgnoreCase)
                || coluna.Equals("SINCRONIZADO_EM", StringComparison.OrdinalIgnoreCase)
                || coluna.Equals("HASH_SHA256", StringComparison.OrdinalIgnoreCase)
                || coluna.Equals("DADOS_JSON", StringComparison.OrdinalIgnoreCase)
                || coluna.Equals("EXCLUIDO", StringComparison.OrdinalIgnoreCase);
        }

        private static string ValorOuPadrao(params string[] valores)
        {
            foreach (var valor in valores)
            {
                if (!string.IsNullOrWhiteSpace(valor))
                    return valor.Trim();
            }

            return string.Empty;
        }

        private static List<T> Chunk<T>(IReadOnlyList<T> items, int size, int offset)
        {
            var count = Math.Min(size, items.Count - offset);
            var chunk = new List<T>(count);
            for (var i = 0; i < count; i++)
                chunk.Add(items[offset + i]);

            return chunk;
        }

        private static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> items, int size)
        {
            for (var offset = 0; offset < items.Count; offset += size)
                yield return Chunk(items, size, offset);
        }
    }
}
