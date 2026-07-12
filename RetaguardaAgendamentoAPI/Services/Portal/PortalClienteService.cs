using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Npgsql;
using RetaguardaAgendamentoAPI.Models.Portal;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace RetaguardaAgendamentoAPI.Services.Portal
{
    public class PortalClienteService
    {
        private static readonly string[] ClienteTabelas = { "CLIENTE", "CLIENTES", "FUNCIONARIO", "FUNCIONARIOS" };
        private static readonly string[] HistoricoTabelas = { "CONSULTA", "CONSULTAS", "AGENDAMENTO", "AGENDAMENTOS" };

        private readonly string _connectionString;
        private readonly string _schema;
        private readonly IMemoryCache _cache;

        private sealed class TabelaMetadata
        {
            public string Tabela { get; set; }
            public HashSet<string> Colunas { get; set; }
        }

        public PortalClienteService(IConfiguration configuration, IMemoryCache cache)
        {
            var adminConnection = configuration.GetConnectionString("AdminConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:AdminConnection nao configurada.");

            _schema = Environment.GetEnvironmentVariable("AGENDA_OPERACIONAL_DATABASE")
                ?? configuration.GetValue<string>("AgendaOperacionalDatabase")
                ?? "agenda_operacional";

            // No Postgres o "banco operacional" e um schema; direcionamos via search_path.
            var builder = new NpgsqlConnectionStringBuilder(adminConnection)
            {
                SearchPath = _schema
            };

            _connectionString = builder.ConnectionString;
            _cache = cache;
        }

        public async Task<IReadOnlyList<PortalClienteResumo>> PesquisarAsync(int empresaId, string nome, string id, string empresa, int limite)
        {
            limite = Math.Clamp(limite <= 0 ? 30 : limite, 1, 100);
            nome = (nome ?? string.Empty).Trim();
            id = (id ?? string.Empty).Trim();
            empresa = (empresa ?? string.Empty).Trim();

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var metadata = await ObterMetadataAsync(connection, "clientes", ClienteTabelas);
            if (metadata == null)
                return Array.Empty<PortalClienteResumo>();

            var tabela = metadata.Tabela;
            var colunas = metadata.Colunas;
            var where = new List<string>
            {
                $"{Ident("ID_EMPRESA")} = @empresaId",
                $"COALESCE({Ident("EXCLUIDO")}, 'N') <> 'S'"
            };

            if (!string.IsNullOrWhiteSpace(nome))
            {
                var camposNome = new[] { "NOME", "DADOS_JSON" }
                    .Where(colunas.Contains)
                    .Select(c => $"LOWER(COALESCE({Ident(c)}, '')) LIKE @nome");

                var filtroNome = string.Join(" OR ", camposNome);
                if (!string.IsNullOrWhiteSpace(filtroNome))
                    where.Add($"({filtroNome})");
            }

            if (!string.IsNullOrWhiteSpace(id))
            {
                var camposId = new[] { "ID_LOCAL", "ID_TEXTO", "CODIGO", "DADOS_JSON" }
                    .Where(colunas.Contains)
                    .Select(c => c.Equals("ID_LOCAL", StringComparison.OrdinalIgnoreCase)
                        ? $"({Ident(c)} = @idExact OR LOWER(COALESCE({Ident(c)}, '')) LIKE @id)"
                        : $"LOWER(COALESCE({Ident(c)}, '')) LIKE @id");

                var filtroId = string.Join(" OR ", camposId);
                if (!string.IsNullOrWhiteSpace(filtroId))
                    where.Add($"({filtroId})");
            }

            if (!string.IsNullOrWhiteSpace(empresa))
            {
                var camposEmpresa = new[] { "EMPRESA", "DADOS_JSON" }
                    .Where(colunas.Contains)
                    .Select(c => $"LOWER(COALESCE({Ident(c)}, '')) LIKE @empresa");

                var filtroEmpresa = string.Join(" OR ", camposEmpresa);
                if (!string.IsNullOrWhiteSpace(filtroEmpresa))
                    where.Add($"({filtroEmpresa})");
            }

            var camposSelect = ColunasResumo(colunas);
            var orderBy = colunas.Contains("NOME") ? $"{Ident("NOME")} ASC" : $"{Ident("SINCRONIZADO_EM")} DESC";
            var sql = $@"
                SELECT {string.Join(", ", camposSelect.Select(Ident))}
                  FROM {Ident(tabela)}
                 WHERE {string.Join(" AND ", where)}
                 ORDER BY {orderBy}
                 LIMIT @limite";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@empresaId", empresaId);
            command.Parameters.AddWithValue("@nome", $"%{nome.ToLowerInvariant()}%");
            command.Parameters.AddWithValue("@idExact", id);
            command.Parameters.AddWithValue("@id", $"%{id.ToLowerInvariant()}%");
            command.Parameters.AddWithValue("@empresa", $"%{empresa.ToLowerInvariant()}%");
            command.Parameters.AddWithValue("@limite", limite);

            var clientes = new List<PortalClienteResumo>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = LerLinha(reader);
                clientes.Add(MontarResumo(row));
            }

            return clientes;
        }

        public async Task<PortalClienteDetalhe> ObterDetalheAsync(int empresaId, string idLocal)
        {
            if (string.IsNullOrWhiteSpace(idLocal))
                throw new ArgumentException("Cliente nao informado.");

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var metadata = await ObterMetadataAsync(connection, "clientes", ClienteTabelas);
            if (metadata == null)
                return null;

            var tabela = metadata.Tabela;
            var camposSelect = ColunasDetalhe(metadata.Colunas);
            await using var command = new NpgsqlCommand($@"
                SELECT {string.Join(", ", camposSelect.Select(Ident))}
                  FROM {Ident(tabela)}
                 WHERE {Ident("ID_EMPRESA")} = @empresaId
                   AND {Ident("ID_LOCAL")} = @idLocal
                   AND COALESCE({Ident("EXCLUIDO")}, 'N') <> 'S'
                 LIMIT 1", connection);
            command.Parameters.AddWithValue("@empresaId", empresaId);
            command.Parameters.AddWithValue("@idLocal", idLocal.Trim());

            Dictionary<string, string> row = null;
            await using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                    row = LerLinha(reader);
            }

            if (row == null)
                return null;

            var detalhe = MontarDetalhe(row);
            detalhe.Historico = await ObterHistoricoAsync(connection, empresaId, detalhe);
            return detalhe;
        }

        private async Task<List<PortalHistoricoItem>> ObterHistoricoAsync(
            NpgsqlConnection connection,
            int empresaId,
            PortalClienteDetalhe cliente)
        {
            var metadata = await ObterMetadataAsync(connection, "historico", HistoricoTabelas);
            if (metadata == null)
                return new List<PortalHistoricoItem>();

            var tabela = metadata.Tabela;
            var colunas = metadata.Colunas;
            var links = new List<string>();

            foreach (var coluna in new[] { "CLIENTE_ID", "ID_CLIENTE", "CLIENTE_ID_LOCAL", "ID_LOCAL_CLIENTE" })
            {
                if (colunas.Contains(coluna))
                    links.Add($"{Ident(coluna)} = @idLocal");
            }

            if (!string.IsNullOrWhiteSpace(cliente.Cpf))
            {
                foreach (var coluna in new[] { "CPF", "CLIENTE_CPF", "FUNCIONARIO_CPF" })
                {
                    if (colunas.Contains(coluna))
                        links.Add($"{Ident(coluna)} = @cpf");
                }
            }

            if (!string.IsNullOrWhiteSpace(cliente.Nome))
            {
                foreach (var coluna in new[] { "CLIENTE_NOME", "FUNCIONARIO", "NOME" })
                {
                    if (colunas.Contains(coluna))
                        links.Add($"LOWER(COALESCE({Ident(coluna)}, '')) = @nome");
                }
            }

            if (colunas.Contains("DADOS_JSON"))
                links.Add($"{Ident("DADOS_JSON")} LIKE @jsonId");

            if (links.Count == 0)
                return new List<PortalHistoricoItem>();

            var orderBy = PrimeiroCampo(colunas, "DATA_CONSULTA", "DATA_AGENDA", "DATA", "SINCRONIZADO_EM") ?? "SINCRONIZADO_EM";
            var camposSelect = ColunasHistorico(colunas);
            var sql = $@"
                SELECT {string.Join(", ", camposSelect.Select(Ident))}
                  FROM {Ident(tabela)}
                 WHERE {Ident("ID_EMPRESA")} = @empresaId
                   AND COALESCE({Ident("EXCLUIDO")}, 'N') <> 'S'
                   AND ({string.Join(" OR ", links)})
                 ORDER BY {Ident(orderBy)} DESC
                 LIMIT 30";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@empresaId", empresaId);
            command.Parameters.AddWithValue("@idLocal", cliente.IdLocal ?? string.Empty);
            command.Parameters.AddWithValue("@cpf", ApenasDigitos(cliente.Cpf));
            command.Parameters.AddWithValue("@nome", (cliente.Nome ?? string.Empty).ToLowerInvariant());
            command.Parameters.AddWithValue("@jsonId", $"%{cliente.IdLocal}%");

            var historico = new List<PortalHistoricoItem>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                historico.Add(MontarHistorico(LerLinha(reader)));

            return historico;
        }

        private static PortalClienteResumo MontarResumo(Dictionary<string, string> row)
        {
            var json = LerJson(row);
            return new PortalClienteResumo
            {
                IdLocal = Valor(row, json, "ID_LOCAL", "Id", "IdLocal"),
                IdCadastro = ValorJson(json, "Id", "IdTexto", "Codigo", "ID_TEXTO", "CODIGO"),
                Nome = Valor(row, json, "NOME", "Nome", "RazaoSocial"),
                Empresa = Valor(row, json, "EMPRESA", "Empresa"),
                Cargo = Valor(row, json, "CARGO", "Cargo"),
                Cpf = FormatarCpf(Valor(row, json, "CPF", "Cpf")),
                Rg = Valor(row, json, "RG", "Rg"),
                Email = Valor(row, json, "EMAIL", "Email"),
                Sexo = Valor(row, json, "SEXO", "Sexo"),
                Telefone = Valor(row, json, "TELEFONE", "FONE", "CELULAR", "Telefone", "Celular"),
                SincronizadoEm = Valor(row, json, "SINCRONIZADO_EM")
            };
        }

        private static PortalClienteDetalhe MontarDetalhe(Dictionary<string, string> row)
        {
            var resumo = MontarResumo(row);
            var json = LerJson(row);
            return new PortalClienteDetalhe
            {
                IdLocal = resumo.IdLocal,
                IdCadastro = resumo.IdCadastro,
                Nome = resumo.Nome,
                Empresa = resumo.Empresa,
                Cargo = resumo.Cargo,
                Cpf = resumo.Cpf,
                Rg = resumo.Rg,
                Email = resumo.Email,
                Sexo = resumo.Sexo,
                Telefone = resumo.Telefone,
                SincronizadoEm = resumo.SincronizadoEm,
                Escolaridade = Valor(row, json, "ESCOLARIDADE", "Escolaridade"),
                EstadoCivil = Valor(row, json, "ESTADO_CIVIL", "EstadoCivil"),
                Naturalidade = Valor(row, json, "NATURALIDADE", "Naturalidade"),
                TipoEndereco = Valor(row, json, "TIPO_ENDERECO", "TipoEndereco"),
                Endereco = Valor(row, json, "ENDERECO", "LOGRADOURO", "Endereco", "Logradouro"),
                Observacoes = Valor(row, json, "OBSERVACOES", "OBS", "Observacoes", "Obs")
            };
        }

        private static PortalHistoricoItem MontarHistorico(Dictionary<string, string> row)
        {
            var json = LerJson(row);
            var trabalhaArmado = Valor(row, json, "TRABALHA_ARMADO", "ARMADO", "TrabalhaArmado");
            return new PortalHistoricoItem
            {
                IdLocal = Valor(row, json, "ID_LOCAL", "Id", "IdLocal"),
                Data = Valor(row, json, "DATA_CONSULTA", "DATA_AGENDA", "DATA", "DataConsulta", "Data", "DataAgenda"),
                Horario = Valor(row, json, "HORARIO", "HORA", "Horario", "Hora"),
                Funcionario = Valor(row, json, "FUNCIONARIO", "CLIENTE_NOME", "Nome", "Funcionario"),
                Empresa = Valor(row, json, "EMPRESA", "CLIENTE_EMPRESA", "Empresa"),
                Cargo = Valor(row, json, "CARGO", "CLIENTE_CARGO", "Cargo"),
                Motivo = Valor(row, json, "MOTIVO", "Motivo"),
                Status = Valor(row, json, "STATUS", "Status"),
                TipoLaudo = EhSim(trabalhaArmado) ? "Com arma" : "Sem arma",
                Observacao = Valor(row, json, "OBSERVACAO", "OBS", "Observacao", "Obs")
            };
        }

        private async Task<TabelaMetadata> ObterMetadataAsync(NpgsqlConnection connection, string categoria, IEnumerable<string> candidatas)
        {
            var cacheKey = $"portal:{_schema}:{categoria}";
            if (_cache.TryGetValue(cacheKey, out TabelaMetadata metadata))
                return metadata;

            var tabela = await PrimeiraTabelaExistenteAsync(connection, candidatas);
            if (tabela == null)
                return null;

            metadata = new TabelaMetadata
            {
                Tabela = tabela,
                Colunas = await ListarColunasAsync(connection, tabela)
            };

            _cache.Set(cacheKey, metadata, TimeSpan.FromMinutes(10));
            return metadata;
        }

        private static async Task<string> PrimeiraTabelaExistenteAsync(NpgsqlConnection connection, IEnumerable<string> candidatas)
        {
            foreach (var tabela in candidatas)
            {
                await using var command = new NpgsqlCommand(@"
                    SELECT COUNT(*)
                      FROM INFORMATION_SCHEMA.TABLES
                     WHERE TABLE_SCHEMA = current_schema()
                       AND LOWER(TABLE_NAME) = LOWER(@tabela)", connection);
                command.Parameters.AddWithValue("@tabela", tabela);

                if (Convert.ToInt32(await command.ExecuteScalarAsync()) > 0)
                    return tabela;
            }

            return null;
        }

        private static async Task<HashSet<string>> ListarColunasAsync(NpgsqlConnection connection, string tabela)
        {
            var colunas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var command = new NpgsqlCommand(@"
                SELECT COLUMN_NAME
                  FROM INFORMATION_SCHEMA.COLUMNS
                 WHERE TABLE_SCHEMA = current_schema()
                   AND LOWER(TABLE_NAME) = LOWER(@tabela)", connection);
            command.Parameters.AddWithValue("@tabela", tabela);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                colunas.Add(reader.GetString(0));

            return colunas;
        }

        private static IReadOnlyList<string> ColunasResumo(HashSet<string> colunas)
        {
            return ColunasExistentes(colunas,
                "ID_LOCAL", "ID_TEXTO", "CODIGO", "NOME", "RAZAO_SOCIAL", "EMPRESA", "CARGO",
                "CPF", "RG", "EMAIL", "SEXO", "TELEFONE", "FONE", "CELULAR", "SINCRONIZADO_EM", "DADOS_JSON");
        }

        private static IReadOnlyList<string> ColunasDetalhe(HashSet<string> colunas)
        {
            return ColunasExistentes(colunas,
                "ID_LOCAL", "ID_TEXTO", "CODIGO", "NOME", "RAZAO_SOCIAL", "EMPRESA", "CARGO",
                "CPF", "RG", "EMAIL", "SEXO", "TELEFONE", "FONE", "CELULAR", "SINCRONIZADO_EM",
                "ESCOLARIDADE", "ESTADO_CIVIL", "NATURALIDADE", "TIPO_ENDERECO", "ENDERECO",
                "LOGRADOURO", "OBSERVACOES", "OBS", "DADOS_JSON");
        }

        private static IReadOnlyList<string> ColunasHistorico(HashSet<string> colunas)
        {
            return ColunasExistentes(colunas,
                "ID_LOCAL", "DATA_CONSULTA", "DATA_AGENDA", "DATA", "HORARIO", "HORA",
                "FUNCIONARIO", "CLIENTE_NOME", "NOME", "EMPRESA", "CLIENTE_EMPRESA",
                "CARGO", "CLIENTE_CARGO", "MOTIVO", "STATUS", "TRABALHA_ARMADO", "ARMADO",
                "OBSERVACAO", "OBS", "DADOS_JSON");
        }

        private static IReadOnlyList<string> ColunasExistentes(HashSet<string> colunas, params string[] candidatas)
        {
            var result = candidatas.Where(colunas.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (result.Count == 0)
                result.Add(colunas.Contains("ID_LOCAL") ? "ID_LOCAL" : colunas.First());

            return result;
        }

        private static string Ident(string nome)
        {
            return "\"" + (nome ?? string.Empty).ToLowerInvariant().Replace("\"", "\"\"") + "\"";
        }

        private static Dictionary<string, string> LerLinha(System.Data.Common.DbDataReader reader)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var valor = reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
                row[reader.GetName(i)] = valor ?? string.Empty;
            }

            return row;
        }

        private static Dictionary<string, string> LerJson(Dictionary<string, string> row)
        {
            var dados = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!row.TryGetValue("DADOS_JSON", out var json) || string.IsNullOrWhiteSpace(json))
                return dados;

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return dados;

                foreach (var property in document.RootElement.EnumerateObject())
                    dados[property.Name] = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString() ?? string.Empty
                        : property.Value.GetRawText();
            }
            catch (JsonException)
            {
                return dados;
            }

            return dados;
        }

        private static string Valor(Dictionary<string, string> row, Dictionary<string, string> json, params string[] nomes)
        {
            foreach (var nome in nomes)
            {
                if (row.TryGetValue(nome, out var valor) && !string.IsNullOrWhiteSpace(valor))
                    return valor.Trim();

                if (json.TryGetValue(nome, out valor) && !string.IsNullOrWhiteSpace(valor))
                    return valor.Trim();
            }

            return string.Empty;
        }

        private static string ValorJson(Dictionary<string, string> json, params string[] nomes)
        {
            foreach (var nome in nomes)
            {
                if (json.TryGetValue(nome, out var valor) && !string.IsNullOrWhiteSpace(valor))
                    return valor.Trim();
            }

            return string.Empty;
        }

        private static string PrimeiroCampo(HashSet<string> colunas, params string[] nomes)
        {
            return nomes.FirstOrDefault(colunas.Contains);
        }

        private static bool EhSim(string valor)
        {
            valor = (valor ?? string.Empty).Trim();
            return valor.Equals("S", StringComparison.OrdinalIgnoreCase)
                || valor.Equals("SIM", StringComparison.OrdinalIgnoreCase)
                || valor.Equals("TRUE", StringComparison.OrdinalIgnoreCase)
                || valor.Equals("1", StringComparison.OrdinalIgnoreCase);
        }

        private static string ApenasDigitos(string valor)
        {
            return new string((valor ?? string.Empty).Where(char.IsDigit).ToArray());
        }

        private static string FormatarCpf(string valor)
        {
            var digitos = ApenasDigitos(valor);
            return digitos.Length == 11
                ? Convert.ToUInt64(digitos).ToString(@"000\.000\.000\-00", CultureInfo.InvariantCulture)
                : valor;
        }
    }
}
