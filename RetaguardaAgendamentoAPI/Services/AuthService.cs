using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using RetaguardaAgendamentoAPI.Models.Auth;
using RetaguardaAgendamentoAPI.Services.Email;
using RetaguardaAgendamentoAPI.Util;

namespace RetaguardaAgendamentoAPI.Services.Auth
{
    public class AuthService
    {
        private const string TipoTokenConfirmacaoEmail = "CONFIRMACAO_EMAIL";
        private const string TipoTokenResetSenha = "RESET_SENHA";

        private readonly string _connectionString;
        private readonly EmailService _emailService;

        public AuthService(IConfiguration configuration, EmailService emailService = null)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection nao configurada.");
            _emailService = emailService;
        }

        public async Task<AuthResponse> CriarContaAsync(CriarContaRequest request)
        {
            ValidarCriacao(request);
            var cnpj = CnpjUtils.Normalizar(request.Cnpj);
            var email = NormalizarLogin(request.Email);
            var login = string.IsNullOrWhiteSpace(request.Login) ? email : NormalizarLogin(request.Login);
            var senha = request.Senha.Trim();
            var perfil = string.IsNullOrWhiteSpace(request.Perfil) ? "Administrador" : request.Perfil.Trim();

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            if (await ObterUsuarioIdPorEmailAsync(connection, email) != null)
                throw new InvalidOperationException("E-mail ja cadastrado no sistema. Use outro e-mail ou recupere a senha.");

            var empresaId = await ObterEmpresaIdAsync(connection, cnpj);
            if (empresaId == null)
            {
                empresaId = await InserirEmpresaAsync(connection, cnpj, request);
            }

            var usuarioExistente = await ObterUsuarioIdAsync(connection, empresaId.Value, login);
            if (usuarioExistente != null)
            {
                var confirmado = await ObterUsuarioConfirmadoAsync(connection, usuarioExistente.Value);
                if (string.Equals(confirmado, "S", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Usuario ja cadastrado para esta empresa.");

                await AtualizarUsuarioPendenteAsync(connection, usuarioExistente.Value, request.UsuarioNome, email, perfil);
                return await MontarRespostaAsync(connection, empresaId.Value, usuarioExistente.Value, string.Empty, null);
            }

            var senhaHash = PasswordHasher.Hash(senha);
            var usuarioId = await InserirUsuarioAsync(connection, empresaId.Value, request.UsuarioNome, login, senhaHash, string.Empty, perfil, email);

            var resposta = await MontarRespostaAsync(connection, empresaId.Value, usuarioId, string.Empty, null);
            return await GerarEnviarConfirmacaoAsync(connection, usuarioId, email, request.UsuarioNome, resposta);
        }

        public async Task<ConfirmacaoEmailResponse> ConfirmarEmailAsync(ConfirmarEmailRequest request)
        {
            if (request == null)
                throw new ArgumentException("Informe os dados de confirmacao.");

            var email = NormalizarLogin(request.Email);
            var codigo = (request.Codigo ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(email) || email.IndexOf('@') <= 0)
                throw new ArgumentException("E-mail valido e obrigatorio.");

            if (string.IsNullOrWhiteSpace(codigo))
                throw new ArgumentException("Codigo de confirmacao obrigatorio.");

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var tokenHash = HashToken(codigo);
            const string sql = @"
                SELECT t.ID AS TokenId, u.ID AS UsuarioId, u.ID_EMPRESA AS EmpresaId
                  FROM RET_EMAIL_TOKEN t
                  JOIN RET_USUARIO u ON u.ID = t.ID_USUARIO
                 WHERE LOWER(u.EMAIL) = @email
                   AND t.TIPO = 'CONFIRMACAO_EMAIL'
                   AND t.TOKEN_HASH = @tokenHash
                   AND t.USADO_EM IS NULL
                   AND t.EXPIRA_EM > UTC_TIMESTAMP()
                   AND u.ATIVO = 'S'
                 LIMIT 1";

            await using var localizar = new MySqlCommand(sql, connection);
            localizar.Parameters.AddWithValue("@email", email);
            localizar.Parameters.AddWithValue("@tokenHash", tokenHash);

            await using var reader = await localizar.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new UnauthorizedAccessException("Codigo de confirmacao invalido ou expirado.");

            var tokenId = Convert.ToInt64(reader["TokenId"]);
            var usuarioId = Convert.ToInt32(reader["UsuarioId"]);
            var empresaId = Convert.ToInt32(reader["EmpresaId"]);
            await reader.CloseAsync();

            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                await ExecutarAsync(connection, (MySqlTransaction)transaction,
                    "UPDATE RET_EMAIL_TOKEN SET USADO_EM = UTC_TIMESTAMP() WHERE ID = @id",
                    ("@id", tokenId));

                await ExecutarAsync(connection, (MySqlTransaction)transaction,
                    "UPDATE RET_USUARIO SET CONFIRMADO = 'S', CONFIRMADO_EM = UTC_TIMESTAMP() WHERE ID = @id",
                    ("@id", usuarioId));

                await ExecutarAsync(connection, (MySqlTransaction)transaction,
                    "UPDATE EMPRESA SET REGISTRADO = 'S', DATA_REGISTRO = COALESCE(DATA_REGISTRO, UTC_DATE()), HORA_REGISTRO = COALESCE(HORA_REGISTRO, DATE_FORMAT(UTC_TIME(), '%H:%i:%s')) WHERE ID = @id",
                    ("@id", empresaId));

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return new ConfirmacaoEmailResponse
            {
                Sucesso = true,
                Mensagem = "E-mail confirmado com sucesso. O login ja pode gerar token de acesso."
            };
        }

        public async Task<ConfirmacaoEmailResponse> ReenviarConfirmacaoAsync(ReenviarConfirmacaoRequest request)
        {
            if (request == null)
                throw new ArgumentException("Informe os dados para reenvio.");

            var email = NormalizarLogin(request.Email);
            if (string.IsNullOrWhiteSpace(email) || email.IndexOf('@') <= 0)
                throw new ArgumentException("E-mail valido e obrigatorio.");

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT u.ID AS UsuarioId, u.NOME, COALESCE(u.CONFIRMADO, 'S') AS CONFIRMADO, COALESCE(e.REGISTRADO, 'S') AS REGISTRADO
                  FROM RET_USUARIO u
                  JOIN EMPRESA e ON e.ID = u.ID_EMPRESA
                 WHERE LOWER(u.EMAIL) = @email
                   AND u.ATIVO = 'S'
                 LIMIT 1";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@email", email);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return new ConfirmacaoEmailResponse
                {
                    Sucesso = true,
                    Mensagem = "Se o e-mail estiver pendente, um novo codigo sera enviado."
                };
            }

            var usuarioId = Convert.ToInt32(reader["UsuarioId"]);
            var nome = reader["NOME"] == DBNull.Value ? string.Empty : reader["NOME"].ToString();
            var confirmado = reader["CONFIRMADO"].ToString();
            var registrado = reader["REGISTRADO"].ToString();
            await reader.CloseAsync();

            if (string.Equals(confirmado, "S", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(registrado, "S", StringComparison.OrdinalIgnoreCase))
            {
                return new ConfirmacaoEmailResponse
                {
                    Sucesso = true,
                    Mensagem = "Esta conta ja esta confirmada."
                };
            }

            return await GerarEnviarConfirmacaoAsync(connection, usuarioId, email, nome);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            if (request == null)
                throw new ArgumentException("Informe os dados de login.");

            var email = NormalizarLogin(request.Email);
            var senha = request.Senha?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(email) || email.IndexOf('@') <= 0 || string.IsNullOrWhiteSpace(senha))
                throw new ArgumentException("E-mail e senha sao obrigatorios.");

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT u.ID, u.ID_EMPRESA, u.SENHA_HASH, u.SENHA_SALT, COALESCE(u.CONFIRMADO, 'S') AS CONFIRMADO, COALESCE(e.REGISTRADO, 'S') AS REGISTRADO
                  FROM RET_USUARIO u
                  JOIN EMPRESA e ON e.ID = u.ID_EMPRESA
                 WHERE LOWER(u.EMAIL) = @email
                   AND u.ATIVO = 'S'
                 LIMIT 1";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@email", email);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new UnauthorizedAccessException("Usuario ou senha invalidos.");

            var usuarioId = Convert.ToInt32(reader["ID"]);
            var empresaId = Convert.ToInt32(reader["ID_EMPRESA"]);
            var senhaHash = reader["SENHA_HASH"].ToString();
            var salt = reader["SENHA_SALT"].ToString();
            var usuarioConfirmado = reader["CONFIRMADO"].ToString();
            var empresaRegistrada = reader["REGISTRADO"].ToString();
            await reader.CloseAsync();

            if (!PasswordHasher.Verificar(senha, senhaHash, salt))
                throw new UnauthorizedAccessException("Usuario ou senha invalidos.");

            // Re-hash transparente: hashes legados SHA-256 sao regravados em PBKDF2 no login bem-sucedido.
            if (PasswordHasher.PrecisaRehash(senhaHash))
                await AtualizarSenhaAsync(connection, usuarioId, PasswordHasher.Hash(senha));

            if (!string.Equals(empresaRegistrada, "S", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(usuarioConfirmado, "S", StringComparison.OrdinalIgnoreCase))
            {
                return await MontarRespostaAsync(connection, empresaId, usuarioId, string.Empty, null);
            }

            await AtualizarUltimoLoginAsync(connection, usuarioId);
            return await CriarSessaoAsync(connection, empresaId, usuarioId);
        }

        public async Task<RecuperarSenhaResponse> RecuperarSenhaAsync(RecuperarSenhaRequest request)
        {
            if (request == null)
                throw new ArgumentException("Informe os dados para recuperacao de senha.");

            var email = NormalizarLogin(request.Email);

            if (string.IsNullOrWhiteSpace(email) || email.IndexOf('@') <= 0)
                throw new ArgumentException("E-mail valido e obrigatorio.");

            // Resposta generica sempre igual, exista o usuario ou nao (anti-enumeracao).
            var respostaGenerica = new RecuperarSenhaResponse
            {
                Sucesso = true,
                Mensagem = "Se o e-mail estiver cadastrado, um codigo de redefinicao sera enviado."
            };

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT u.ID, u.NOME
                  FROM RET_USUARIO u
                  JOIN EMPRESA e ON e.ID = u.ID_EMPRESA
                 WHERE LOWER(u.EMAIL) = @email
                   AND u.ATIVO = 'S'
                   AND COALESCE(e.REGISTRADO, 'S') = 'S'
                   AND COALESCE(u.CONFIRMADO, 'S') = 'S'
                 LIMIT 1";

            await using var localizar = new MySqlCommand(sql, connection);
            localizar.Parameters.AddWithValue("@email", email);

            int usuarioId;
            string nome;
            await using (var reader = await localizar.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                    return respostaGenerica;

                usuarioId = Convert.ToInt32(reader["ID"]);
                nome = reader["NOME"] == DBNull.Value ? string.Empty : reader["NOME"].ToString();
            }

            var codigo = CriarCodigoConfirmacao();
            var expiraEm = DateTime.UtcNow.AddMinutes(30);
            await RegistrarTokenAsync(connection, usuarioId, TipoTokenResetSenha, codigo, expiraEm);

            if (_emailService?.ReturnConfirmationCodeInResponse == true)
                respostaGenerica.CodigoResetTeste = codigo;

            if (_emailService?.Enabled == true)
                await _emailService.EnviarCodigoRedefinicaoSenhaAsync(email, nome, codigo);

            return respostaGenerica;
        }

        public async Task<RedefinirSenhaResponse> RedefinirSenhaAsync(RedefinirSenhaRequest request)
        {
            if (request == null)
                throw new ArgumentException("Informe os dados para redefinicao de senha.");

            var email = NormalizarLogin(request.Email);
            var codigo = (request.Codigo ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(email) || email.IndexOf('@') <= 0)
                throw new ArgumentException("E-mail valido e obrigatorio.");

            if (string.IsNullOrWhiteSpace(codigo))
                throw new ArgumentException("Codigo de redefinicao obrigatorio.");

            if (string.IsNullOrWhiteSpace(request.NovaSenha) || request.NovaSenha.Trim().Length < 8)
                throw new ArgumentException("Nova senha deve possuir pelo menos 8 caracteres.");

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var tokenHash = HashToken(codigo);
            const string sql = @"
                SELECT t.ID AS TokenId, u.ID AS UsuarioId
                  FROM RET_EMAIL_TOKEN t
                  JOIN RET_USUARIO u ON u.ID = t.ID_USUARIO
                 WHERE LOWER(u.EMAIL) = @email
                   AND t.TIPO = @tipo
                   AND t.TOKEN_HASH = @tokenHash
                   AND t.USADO_EM IS NULL
                   AND t.EXPIRA_EM > UTC_TIMESTAMP()
                   AND u.ATIVO = 'S'
                 LIMIT 1";

            await using var localizar = new MySqlCommand(sql, connection);
            localizar.Parameters.AddWithValue("@email", email);
            localizar.Parameters.AddWithValue("@tipo", TipoTokenResetSenha);
            localizar.Parameters.AddWithValue("@tokenHash", tokenHash);

            long tokenId;
            int usuarioId;
            await using (var reader = await localizar.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                    throw new UnauthorizedAccessException("Codigo de redefinicao invalido ou expirado.");

                tokenId = Convert.ToInt64(reader["TokenId"]);
                usuarioId = Convert.ToInt32(reader["UsuarioId"]);
            }

            var senhaHash = PasswordHasher.Hash(request.NovaSenha.Trim());

            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                await ExecutarAsync(connection, (MySqlTransaction)transaction,
                    "UPDATE RET_EMAIL_TOKEN SET USADO_EM = UTC_TIMESTAMP() WHERE ID = @id",
                    ("@id", tokenId));

                await ExecutarAsync(connection, (MySqlTransaction)transaction,
                    "UPDATE RET_USUARIO SET SENHA_HASH = @senhaHash, SENHA_SALT = '' WHERE ID = @id",
                    ("@senhaHash", senhaHash), ("@id", usuarioId));

                // Redefinir a senha derruba todas as sessoes ativas do usuario.
                await ExecutarAsync(connection, (MySqlTransaction)transaction,
                    "UPDATE RET_SESSAO SET REVOGADO = 'S' WHERE ID_USUARIO = @id AND REVOGADO = 'N'",
                    ("@id", usuarioId));

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return new RedefinirSenhaResponse
            {
                Sucesso = true,
                Mensagem = "Senha redefinida com sucesso. Faca login com a nova senha."
            };
        }

        public async Task<AuthResponse> ValidarTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new UnauthorizedAccessException("Token nao informado.");

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var tokenHash = HashToken(token);
            const string sql = @"
                SELECT u.ID AS UsuarioId, u.ID_EMPRESA AS EmpresaId
                  FROM RET_SESSAO s
                  JOIN RET_USUARIO u ON u.ID = s.ID_USUARIO
                  JOIN EMPRESA e ON e.ID = u.ID_EMPRESA
                 WHERE s.TOKEN_HASH = @tokenHash
                   AND s.REVOGADO = 'N'
                   AND s.EXPIRA_EM > UTC_TIMESTAMP()
                   AND u.ATIVO = 'S'
                   AND COALESCE(u.CONFIRMADO, 'S') = 'S'
                   AND COALESCE(e.REGISTRADO, 'S') = 'S'
                  LIMIT 1";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@tokenHash", tokenHash);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new UnauthorizedAccessException("Token invalido ou expirado.");

            var usuarioId = Convert.ToInt32(reader["UsuarioId"]);
            var empresaId = Convert.ToInt32(reader["EmpresaId"]);
            await reader.CloseAsync();

            return await MontarRespostaAsync(connection, empresaId, usuarioId, token, null);
        }

        private static void ValidarCriacao(CriarContaRequest request)
        {
            if (request == null)
                throw new ArgumentException("Informe os dados da conta.");

            if (!CnpjUtils.IsValido(request.Cnpj))
                throw new ArgumentException("CNPJ invalido.");

            if (string.IsNullOrWhiteSpace(request.Email) || request.Email.IndexOf('@') <= 0)
                throw new ArgumentException("E-mail valido e obrigatorio.");

            if (string.IsNullOrWhiteSpace(request.Senha) || request.Senha.Trim().Length < 8)
                throw new ArgumentException("Senha deve possuir pelo menos 8 caracteres.");
        }

        private static async Task ExecutarAsync(MySqlConnection connection, MySqlTransaction transaction, string sql, params (string Nome, object Valor)[] parametros)
        {
            await using var command = new MySqlCommand(sql, connection, transaction);
            foreach (var parametro in parametros)
                command.Parameters.AddWithValue(parametro.Nome, parametro.Valor ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }

        private static async Task<int?> ObterEmpresaIdAsync(MySqlConnection connection, string cnpj)
        {
            await using var command = new MySqlCommand("SELECT ID FROM EMPRESA WHERE CNPJ = @cnpj LIMIT 1", connection);
            command.Parameters.AddWithValue("@cnpj", cnpj);
            var result = await command.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? null : Convert.ToInt32(result);
        }

        private static async Task<int> InserirEmpresaAsync(MySqlConnection connection, string cnpj, CriarContaRequest request)
        {
            const string sql = @"
                INSERT INTO EMPRESA
                    (RAZAO_SOCIAL, NOME_FANTASIA, CNPJ, EMAIL, REGISTRADO, DATA_REGISTRO, HORA_REGISTRO)
                VALUES
                    (@razaoSocial, @nomeFantasia, @cnpj, @email, 'P', NULL, NULL);
                SELECT LAST_INSERT_ID();";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@razaoSocial", ValorOuPadrao(request.RazaoSocial, request.NomeFantasia, "Empresa Agenda"));
            command.Parameters.AddWithValue("@nomeFantasia", ValorOuPadrao(request.NomeFantasia, request.RazaoSocial, "Empresa Agenda"));
            command.Parameters.AddWithValue("@cnpj", cnpj);
            command.Parameters.AddWithValue("@email", (object)request.Email ?? DBNull.Value);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        private static async Task<int?> ObterUsuarioIdAsync(MySqlConnection connection, int empresaId, string login)
        {
            await using var command = new MySqlCommand("SELECT ID FROM RET_USUARIO WHERE ID_EMPRESA = @empresaId AND LOWER(LOGIN) = @login LIMIT 1", connection);
            command.Parameters.AddWithValue("@empresaId", empresaId);
            command.Parameters.AddWithValue("@login", login);
            var result = await command.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? null : Convert.ToInt32(result);
        }

        private static async Task<int?> ObterUsuarioIdPorEmailAsync(MySqlConnection connection, string email)
        {
            await using var command = new MySqlCommand("SELECT ID FROM RET_USUARIO WHERE LOWER(EMAIL) = @email LIMIT 1", connection);
            command.Parameters.AddWithValue("@email", email);
            var result = await command.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? null : Convert.ToInt32(result);
        }

        private static async Task<string> ObterUsuarioConfirmadoAsync(MySqlConnection connection, int usuarioId)
        {
            await using var command = new MySqlCommand("SELECT COALESCE(CONFIRMADO, 'S') FROM RET_USUARIO WHERE ID = @id LIMIT 1", connection);
            command.Parameters.AddWithValue("@id", usuarioId);
            return (await command.ExecuteScalarAsync())?.ToString() ?? "S";
        }

        private static async Task AtualizarUsuarioPendenteAsync(MySqlConnection connection, int usuarioId, string nome, string email, string perfil)
        {
            await using var command = new MySqlCommand(@"
                UPDATE RET_USUARIO
                   SET NOME = @nome,
                       EMAIL = @email,
                       PERFIL = @perfil
                 WHERE ID = @usuarioId
                   AND COALESCE(CONFIRMADO, 'S') <> 'S'", connection);
            command.Parameters.AddWithValue("@nome", ValorOuPadrao(nome, "Usuario"));
            command.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(email) ? DBNull.Value : (object)email);
            command.Parameters.AddWithValue("@perfil", perfil);
            command.Parameters.AddWithValue("@usuarioId", usuarioId);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<int> InserirUsuarioAsync(MySqlConnection connection, int empresaId, string nome, string login, string senhaHash, string salt, string perfil, string email)
        {
            const string sql = @"
                INSERT INTO RET_USUARIO
                    (ID_EMPRESA, NOME, LOGIN, EMAIL, SENHA_HASH, SENHA_SALT, PERFIL, CONFIRMADO, ATIVO)
                VALUES
                    (@empresaId, @nome, @login, @email, @senhaHash, @salt, @perfil, 'P', 'S');
                SELECT LAST_INSERT_ID();";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@empresaId", empresaId);
            command.Parameters.AddWithValue("@nome", ValorOuPadrao(nome, login, "Administrador"));
            command.Parameters.AddWithValue("@login", login);
            command.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(email) ? DBNull.Value : (object)email);
            command.Parameters.AddWithValue("@senhaHash", senhaHash);
            command.Parameters.AddWithValue("@salt", salt);
            command.Parameters.AddWithValue("@perfil", perfil);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        private static async Task AtualizarSenhaAsync(MySqlConnection connection, int usuarioId, string senhaHash)
        {
            await using var command = new MySqlCommand(
                "UPDATE RET_USUARIO SET SENHA_HASH = @senhaHash, SENHA_SALT = '' WHERE ID = @id", connection);
            command.Parameters.AddWithValue("@senhaHash", senhaHash);
            command.Parameters.AddWithValue("@id", usuarioId);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task AtualizarUltimoLoginAsync(MySqlConnection connection, int usuarioId)
        {
            await using var command = new MySqlCommand("UPDATE RET_USUARIO SET ULTIMO_LOGIN = UTC_TIMESTAMP() WHERE ID = @id", connection);
            command.Parameters.AddWithValue("@id", usuarioId);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<AuthResponse> CriarSessaoAsync(MySqlConnection connection, int empresaId, int usuarioId)
        {
            var token = CriarTokenSeguro(48);
            var expiraEm = DateTime.UtcNow.AddHours(12);
            var tokenHash = HashToken(token);

            await using var command = new MySqlCommand(@"
                INSERT INTO RET_SESSAO (ID_USUARIO, TOKEN_HASH, EXPIRA_EM, REVOGADO)
                VALUES (@usuarioId, @tokenHash, @expiraEm, 'N')", connection);
            command.Parameters.AddWithValue("@usuarioId", usuarioId);
            command.Parameters.AddWithValue("@tokenHash", tokenHash);
            command.Parameters.AddWithValue("@expiraEm", expiraEm);
            await command.ExecuteNonQueryAsync();

            return await MontarRespostaAsync(connection, empresaId, usuarioId, token, expiraEm);
        }

        private static async Task<AuthResponse> MontarRespostaAsync(MySqlConnection connection, int empresaId, int usuarioId, string token, DateTime? expiraEm)
        {
            const string sql = @"
                SELECT
                    u.ID AS UsuarioId, u.NOME, u.LOGIN, u.EMAIL, u.PERFIL, COALESCE(u.CONFIRMADO, 'S') AS CONFIRMADO,
                    e.ID AS EmpresaId, e.CNPJ, e.RAZAO_SOCIAL, e.NOME_FANTASIA, e.REGISTRADO
                FROM RET_USUARIO u
                JOIN EMPRESA e ON e.ID = u.ID_EMPRESA
                WHERE u.ID = @usuarioId AND e.ID = @empresaId";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@usuarioId", usuarioId);
            command.Parameters.AddWithValue("@empresaId", empresaId);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new InvalidOperationException("Sessao criada, mas usuario/empresa nao foram localizados.");

            var cnpj = reader["CNPJ"].ToString();
            return new AuthResponse
            {
                Token = token,
                ExpiraEm = expiraEm ?? DateTime.UtcNow.AddHours(12),
                Usuario = new AuthUsuarioResponse
                {
                    Id = Convert.ToInt32(reader["UsuarioId"]),
                    Nome = reader["NOME"].ToString(),
                    Login = reader["LOGIN"].ToString(),
                    Perfil = reader["PERFIL"].ToString(),
                    Email = reader["EMAIL"] == DBNull.Value ? string.Empty : reader["EMAIL"].ToString(),
                    Confirmado = reader["CONFIRMADO"].ToString()
                },
                Empresa = new AuthEmpresaResponse
                {
                    Id = Convert.ToInt32(reader["EmpresaId"]),
                    Cnpj = cnpj,
                    RazaoSocial = reader["RAZAO_SOCIAL"].ToString(),
                    NomeFantasia = reader["NOME_FANTASIA"].ToString(),
                    Registrado = reader["REGISTRADO"].ToString(),
                    BancoOperacional = NomeBancoOperacional(cnpj)
                }
            };
        }

        public static string NomeBancoOperacional(string cnpj)
        {
            var overrideDatabase = Environment.GetEnvironmentVariable("AGENDA_OPERACIONAL_DATABASE");
            if (!string.IsNullOrWhiteSpace(overrideDatabase))
                return overrideDatabase.Trim();

            return "agenda_operacional";
        }

        private async Task<AuthResponse> GerarEnviarConfirmacaoAsync(MySqlConnection connection, int usuarioId, string email, string nome, AuthResponse resposta)
        {
            var confirmacao = await GerarEnviarConfirmacaoAsync(connection, usuarioId, email, nome);
            resposta.EmailConfirmacaoEnviado = confirmacao.Mensagem.Contains("enviado", StringComparison.OrdinalIgnoreCase);
            resposta.Mensagem = confirmacao.Mensagem;
            resposta.CodigoConfirmacaoTeste = confirmacao.CodigoConfirmacaoTeste;
            return resposta;
        }

        private async Task<ConfirmacaoEmailResponse> GerarEnviarConfirmacaoAsync(MySqlConnection connection, int usuarioId, string email, string nome)
        {
            var codigo = CriarCodigoConfirmacao();
            var expiraEm = DateTime.UtcNow.AddMinutes(30);

            await RegistrarTokenAsync(connection, usuarioId, TipoTokenConfirmacaoEmail, codigo, expiraEm);

            var response = new ConfirmacaoEmailResponse
            {
                Sucesso = true,
                Mensagem = "Codigo de confirmacao gerado. Configure o SMTP para envio por e-mail."
            };

            if (_emailService?.ReturnConfirmationCodeInResponse == true)
                response.CodigoConfirmacaoTeste = codigo;

            if (_emailService?.Enabled == true)
            {
                await _emailService.EnviarCodigoConfirmacaoAsync(email, nome, codigo);
                response.Mensagem = "Codigo de confirmacao enviado por e-mail.";
            }

            return response;
        }

        private static async Task RegistrarTokenAsync(MySqlConnection connection, int usuarioId, string tipo, string codigo, DateTime expiraEm)
        {
            await using var revogarAntigos = new MySqlCommand(@"
                UPDATE RET_EMAIL_TOKEN
                   SET USADO_EM = UTC_TIMESTAMP()
                 WHERE ID_USUARIO = @usuarioId
                   AND TIPO = @tipo
                   AND USADO_EM IS NULL", connection);
            revogarAntigos.Parameters.AddWithValue("@usuarioId", usuarioId);
            revogarAntigos.Parameters.AddWithValue("@tipo", tipo);
            await revogarAntigos.ExecuteNonQueryAsync();

            await using var inserir = new MySqlCommand(@"
                INSERT INTO RET_EMAIL_TOKEN
                    (ID_USUARIO, TIPO, TOKEN_HASH, EXPIRA_EM)
                VALUES
                    (@usuarioId, @tipo, @tokenHash, @expiraEm)", connection);
            inserir.Parameters.AddWithValue("@usuarioId", usuarioId);
            inserir.Parameters.AddWithValue("@tipo", tipo);
            inserir.Parameters.AddWithValue("@tokenHash", HashToken(codigo));
            inserir.Parameters.AddWithValue("@expiraEm", expiraEm);
            await inserir.ExecuteNonQueryAsync();
        }

        private static string CriarCodigoConfirmacao()
        {
            return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        }

        private static string NormalizarLogin(string login)
        {
            return (login ?? string.Empty).Trim().ToLowerInvariant();
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



        private static string CriarTokenSeguro(int bytes)
        {
            var buffer = RandomNumberGenerator.GetBytes(bytes);
            return Convert.ToBase64String(buffer)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}

