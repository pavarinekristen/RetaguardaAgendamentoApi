using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RetaguardaAgendamentoAPI.Models.Auth;
using RetaguardaAgendamentoAPI.Services.Auth;
using RetaguardaAgendamentoAPI.Services.Email;
using RetaguardaAgendamentoAPI.Util;
using Testcontainers.PostgreSql;
using Xunit;

namespace RetaguardaAgendamentoAPI.Tests.Auth;

/// <summary>
/// Fixture compartilhada: sobe UM container MySQL para toda a classe de testes.
/// Cada teste usa emails Ãºnicos, por isso nÃ£o hÃ¡ conflito sem necessidade de limpar entre testes.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithDatabase("retaguarda_agendamento")
        .WithUsername("test_user")
        .WithPassword("test_pass")
        .Build();

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        // O AuthService usa identificadores sem schema; o Search Path resolve para
        // retaguarda_agendamento, igual a connection string real da API.
        ConnectionString = _container.GetConnectionString() + ";Search Path=retaguarda_agendamento";
        await CriarSchemaAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// O schema e responsabilidade das migrations (postgres-migrations/001_baseline_schema.sql);
    /// a API nao cria mais tabelas em runtime. Aqui replicamos as tabelas de auth do baseline
    /// no banco do container.
    /// </summary>
    private async Task CriarSchemaAsync()
    {
        const string ddl = @"
            CREATE SCHEMA IF NOT EXISTS retaguarda_agendamento;

            CREATE TABLE IF NOT EXISTS retaguarda_agendamento.empresa (
              id                  SERIAL PRIMARY KEY,
              razao_social        VARCHAR(150),
              nome_fantasia       VARCHAR(150),
              cnpj                VARCHAR(14),
              inscricao_estadual  VARCHAR(30),
              inscricao_municipal VARCHAR(30),
              tipo_regime         CHAR(1),
              crt                 CHAR(1),
              data_constituicao   DATE,
              tipo                CHAR(1),
              email               VARCHAR(250),
              logradouro          VARCHAR(250),
              numero              VARCHAR(10),
              complemento         VARCHAR(100),
              cep                 VARCHAR(8),
              bairro              VARCHAR(100),
              cidade              VARCHAR(100),
              uf                  CHAR(2),
              fone                VARCHAR(15),
              contato             VARCHAR(30),
              codigo_ibge_cidade  INTEGER,
              codigo_ibge_uf      INTEGER,
              logotipo            TEXT,
              registrado          CHAR(1) DEFAULT 'P',
              natureza_juridica   VARCHAR(200),
              simei               CHAR(1),
              email_pagamento     VARCHAR(250),
              data_registro       DATE,
              hora_registro       VARCHAR(8),
              CONSTRAINT uk_empresa_cnpj UNIQUE (cnpj)
            );

            CREATE TABLE IF NOT EXISTS retaguarda_agendamento.ret_usuario (
              id            SERIAL PRIMARY KEY,
              id_empresa    INTEGER NOT NULL,
              nome          VARCHAR(150),
              login         VARCHAR(80) NOT NULL,
              email         VARCHAR(180),
              senha_hash    VARCHAR(128) NOT NULL,
              senha_salt    VARCHAR(64) NOT NULL,
              perfil        VARCHAR(30) NOT NULL DEFAULT 'Administrador',
              confirmado    CHAR(1) NOT NULL DEFAULT 'P',
              confirmado_em TIMESTAMP,
              ativo         CHAR(1) NOT NULL DEFAULT 'S',
              criado_em     TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
              ultimo_login  TIMESTAMP,
              CONSTRAINT uk_ret_usuario_empresa_login UNIQUE (id_empresa, login),
              CONSTRAINT uk_ret_usuario_email         UNIQUE (email),
              CONSTRAINT fk_ret_usuario_empresa FOREIGN KEY (id_empresa)
                  REFERENCES retaguarda_agendamento.empresa (id)
            );

            CREATE TABLE IF NOT EXISTS retaguarda_agendamento.ret_sessao (
              id         SERIAL PRIMARY KEY,
              id_usuario INTEGER NOT NULL,
              token_hash VARCHAR(128) NOT NULL,
              criado_em  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
              expira_em  TIMESTAMP NOT NULL,
              revogado   CHAR(1) NOT NULL DEFAULT 'N',
              CONSTRAINT uk_ret_sessao_token UNIQUE (token_hash),
              CONSTRAINT fk_ret_sessao_usuario FOREIGN KEY (id_usuario)
                  REFERENCES retaguarda_agendamento.ret_usuario (id)
            );

            CREATE TABLE IF NOT EXISTS retaguarda_agendamento.ret_email_token (
              id         BIGSERIAL PRIMARY KEY,
              id_usuario INTEGER NOT NULL,
              tipo       VARCHAR(40) NOT NULL,
              token_hash VARCHAR(128) NOT NULL,
              criado_em  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
              expira_em  TIMESTAMP NOT NULL,
              usado_em   TIMESTAMP,
              CONSTRAINT fk_ret_email_token_usuario FOREIGN KEY (id_usuario)
                  REFERENCES retaguarda_agendamento.ret_usuario (id)
            );
            CREATE INDEX IF NOT EXISTS ix_ret_email_token_usuario_tipo
                ON retaguarda_agendamento.ret_email_token (id_usuario, tipo);
            CREATE INDEX IF NOT EXISTS ix_ret_email_token_hash
                ON retaguarda_agendamento.ret_email_token (token_hash);";

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(ddl, connection);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// Testes de integraÃ§Ã£o do AuthService â€” cobre login, criaÃ§Ã£o de conta,
/// recuperaÃ§Ã£o de senha, validaÃ§Ã£o de token e unicidade global de email (multitenant).
/// </summary>
public class AuthServiceTests : IClassFixture<PostgresFixture>
{
    private readonly AuthService _service;
    private readonly string _cs;

    public AuthServiceTests(PostgresFixture fixture)
    {
        _cs = fixture.ConnectionString;
        _service = Build(_cs);
    }

    private static AuthService Build(string connectionString)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                // Envio SMTP desabilitado; codigos de confirmacao/reset voltam na resposta (modo teste).
                ["Email:Enabled"] = "false",
                ["Email:ReturnConfirmationCodeInResponse"] = "true"
            })
            .Build();
        var emailService = new EmailService(config, NullLogger<EmailService>.Instance);
        return new AuthService(config, emailService);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // CRIAR CONTA
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task CriarConta_DadosValidos_RetornaUsuarioComEmailECnpj()
    {
        var req = Conta("valid_create@test.com");
        var result = await _service.CriarContaAsync(req);

        Assert.NotNull(result);
        Assert.Equal(req.Email.ToLower(), result.Usuario.Email.ToLower());
        Assert.Equal(Normaliza(req.Cnpj), result.Empresa.Cnpj);
    }

    [Fact]
    public async Task CriarConta_LoginOmitido_UsaEmailComoLogin()
    {
        var req = Conta("loginvazio@test.com");
        req.Login = "";
        var result = await _service.CriarContaAsync(req);

        Assert.Equal(req.Email.ToLower(), result.Usuario.Login.ToLower());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("semarroba")]
    [InlineData("@semlocal")]
    [InlineData(null)]
    public async Task CriarConta_EmailInvalido_LancaArgumentException(string? email)
    {
        var req = Conta("irrelevante@test.com");
        req.Email = email!;
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CriarContaAsync(req));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1234567890123")]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("ABCD1234EFGH00")]   // DV incorreto
    [InlineData("ABCD1234EFGHAA")]   // DV nao numerico
    [InlineData("00000000000000")]   // tudo zeros
    public async Task CriarConta_CnpjInvalido_LancaArgumentException(string cnpj)
    {
        var req = Conta("cnpjinvalido@test.com");
        req.Cnpj = cnpj;
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CriarContaAsync(req));
    }

    [Fact]
    public async Task CriarConta_CnpjAlfanumerico_Sucesso()
    {
        // ABCD1234EFGH46: base ABCD1234EFGH, DV calculado = 46
        var req = Conta("alfa_cnpj@test.com", "ABCD1234EFGH46");
        var result = await _service.CriarContaAsync(req);

        Assert.NotNull(result);
        Assert.Equal("ABCD1234EFGH46", result.Empresa.Cnpj);
    }

    [Fact]
    public async Task CriarConta_CnpjAlfanumericoFormatado_NormalizaEAceita()
    {
        // PM.0O3.6A7/0001-71 Ã© o CNPJ do ticket Jira PDV-40 â€” deve ser aceito formatado
        var req = Conta("alfa_formatado@test.com", "PM.0O3.6A7/0001-71");
        var result = await _service.CriarContaAsync(req);

        Assert.Equal("PM0O36A7000171", result.Empresa.Cnpj);
    }

    [Fact]
    public async Task CriarConta_CnpjAlfanumericoMinusculas_NormalizaEAceita()
    {
        var req = Conta("alfa_lower@test.com", "abcd1234efgh46");
        var result = await _service.CriarContaAsync(req);

        Assert.Equal("ABCD1234EFGH46", result.Empresa.Cnpj);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("abc")]
    public async Task CriarConta_SenhaCurta_LancaArgumentException(string senha)
    {
        var req = Conta("senhacurta@test.com");
        req.Senha = senha;
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CriarContaAsync(req));
    }

    [Fact]
    public async Task CriarConta_EmailDuplicado_CnpjsDiferentes_LancaInvalidOperationException()
    {
        await _service.CriarContaAsync(Conta("dupA@test.com", "11222333000181"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CriarContaAsync(Conta("dupA@test.com", "99888777000100")));
    }

    [Fact]
    public async Task CriarConta_EmailDuplicado_MesmoCnpj_LancaInvalidOperationException()
    {
        await _service.CriarContaAsync(Conta("dupB@test.com", "11222333000181"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CriarContaAsync(Conta("dupB@test.com", "11222333000181")));
    }

    [Fact]
    public async Task CriarConta_UsuarioJaConfirmado_LancaInvalidOperationException()
    {
        var req = Conta("jaconfirmado@test.com", "11222333000181");
        await _service.CriarContaAsync(req);
        await ConfirmarEmpresaEUsuario(req.Email, Normaliza(req.Cnpj));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CriarContaAsync(req));
    }

    [Fact]
    public async Task CriarConta_GarantirTabelas_Idempotente_MultiplasChamadas()
    {
        var s2 = Build(_cs);
        // GarantirTabelasAsync + GarantirColunasUsuarioAsync idempotentes
        await _service.CriarContaAsync(Conta("idem1@test.com", "11222333000181"));
        await s2.CriarContaAsync(Conta("idem2@test.com", "22333444000181"));
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // LOGIN
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Login_EmailESenhaCorretos_RetornaTokenValido()
    {
        var req = Conta("login_ok@test.com", "11222333000181");
        await _service.CriarContaAsync(req);
        await ConfirmarEmpresaEUsuario(req.Email, Normaliza(req.Cnpj));

        var result = await _service.LoginAsync(new LoginRequest
        {
            Email = req.Email,
            Senha = req.Senha
        });

        Assert.NotEmpty(result.Token);
        Assert.True(result.ExpiraEm > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_EmailMaiusculo_CaseInsensitive()
    {
        var req = Conta("case@test.com", "11222333000181");
        await _service.CriarContaAsync(req);
        await ConfirmarEmpresaEUsuario(req.Email, Normaliza(req.Cnpj));

        var result = await _service.LoginAsync(new LoginRequest
        {
            Email = req.Email.ToUpper(),
            Senha = req.Senha
        });

        Assert.NotEmpty(result.Token);
    }

    [Fact]
    public async Task Login_SenhaErrada_LancaUnauthorizedAccessException()
    {
        var req = Conta("errado@test.com", "11222333000181");
        await _service.CriarContaAsync(req);
        await ConfirmarEmpresaEUsuario(req.Email, Normaliza(req.Cnpj));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.LoginAsync(new LoginRequest
            {
                Email = req.Email,
                Senha = "SenhaErrada!"
            }));
    }

    [Fact]
    public async Task Login_EmailNaoCadastrado_LancaUnauthorizedAccessException()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.LoginAsync(new LoginRequest
            {
                Email = "fantasma@test.com",
                Senha = "Qualquer@1"
            }));
    }

    [Theory]
    [InlineData("", "Senha@123")]
    [InlineData("invalido", "Senha@123")]
    [InlineData("@semlocal", "Senha@123")]
    [InlineData("ok@test.com", "")]
    [InlineData("ok@test.com", "   ")]
    public async Task Login_DadosInvalidos_LancaArgumentException(string email, string senha)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.LoginAsync(new LoginRequest { Email = email, Senha = senha }));
    }

    [Fact]
    public async Task Login_UsuarioInativo_LancaUnauthorizedAccessException()
    {
        var req = Conta("inativo@test.com", "11222333000181");
        await _service.CriarContaAsync(req);
        await DesativarUsuario(req.Email);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.LoginAsync(new LoginRequest { Email = req.Email, Senha = req.Senha }));
    }

    [Fact]
    public async Task Login_EmpresaPendente_RetornaSessionSemToken()
    {
        // RecÃ©m-criada: REGISTRADO='P', CONFIRMADO='P' â†’ sem token
        var req = Conta("pendente@test.com", "11222333000181");
        await _service.CriarContaAsync(req);

        var result = await _service.LoginAsync(new LoginRequest
        {
            Email = req.Email,
            Senha = req.Senha
        });

        Assert.Empty(result.Token);
    }

    [Fact]
    public async Task Login_HashLegadoSha256_AutenticaEFazRehashParaPbkdf2()
    {
        var req = Conta("legado_rehash@test.com", "11222333000181");
        await _service.CriarContaAsync(req);
        await ConfirmarEmpresaEUsuario(req.Email, Normaliza(req.Cnpj));

        // Regrava o usuario com hash no formato legado SHA256(salt:senha).
        const string saltLegado = "salt-legado-teste";
        var hashLegado = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{saltLegado}:{req.Senha}"))).ToLowerInvariant();
        await AtualizarHashDireto(req.Email, hashLegado, saltLegado);

        var login = await _service.LoginAsync(new LoginRequest
        {
            Email = req.Email,
            Senha = req.Senha
        });
        Assert.NotEmpty(login.Token);

        // Apos o login, o hash deve ter sido regravado em PBKDF2 e a senha continua valida.
        var hashAtual = await LerHashDireto(req.Email);
        Assert.StartsWith("PBKDF2$", hashAtual);

        var loginNovamente = await _service.LoginAsync(new LoginRequest
        {
            Email = req.Email,
            Senha = req.Senha
        });
        Assert.NotEmpty(loginNovamente.Token);
    }

    [Fact]
    public async Task CriarConta_GravaSenhaEmFormatoPbkdf2()
    {
        var req = Conta("formato_pbkdf2@test.com", "11222333000181");
        await _service.CriarContaAsync(req);

        var hash = await LerHashDireto(req.Email);
        Assert.StartsWith("PBKDF2$", hash);
    }

    [Fact]
    public async Task Login_RetornaEmpresaCorretaDoUsuario()
    {
        var req = Conta("empresa_check@test.com", "11222333000181");
        await _service.CriarContaAsync(req);
        await ConfirmarEmpresaEUsuario(req.Email, Normaliza(req.Cnpj));

        var result = await _service.LoginAsync(new LoginRequest
        {
            Email = req.Email,
            Senha = req.Senha
        });

        Assert.Equal(Normaliza(req.Cnpj), result.Empresa.Cnpj);
        Assert.Equal(req.Email.ToLower(), result.Usuario.Email.ToLower());
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // RECUPERAR SENHA
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task RecuperarSenha_EmailCadastrado_GeraCodigoENaoAlteraSenha()
    {
        var req = Conta("recover@test.com", "11222333000181");
        await _service.CriarContaAsync(req);
        await ConfirmarEmpresaEUsuario(req.Email, Normaliza(req.Cnpj));

        var result = await _service.RecuperarSenhaAsync(new RecuperarSenhaRequest
        {
            Email = req.Email
        });

        Assert.True(result.Sucesso);
        Assert.NotEmpty(result.CodigoResetTeste!);

        // Solicitar reset NAO pode derrubar a senha atual (anti account-DoS).
        var login = await _service.LoginAsync(new LoginRequest
        {
            Email = req.Email,
            Senha = req.Senha
        });
        Assert.NotEmpty(login.Token);
    }

    [Fact]
    public async Task RecuperarSenha_EmailNaoCadastrado_RetornaRespostaGenericaSemCodigo()
    {
        // Resposta genÃ©rica por seguranÃ§a â€” nÃ£o revela se email existe
        var result = await _service.RecuperarSenhaAsync(new RecuperarSenhaRequest
        {
            Email = "naoexiste@test.com"
        });

        Assert.True(result.Sucesso);
        Assert.Null(result.CodigoResetTeste);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalido")]
    [InlineData("@semlocal")]
    [InlineData(null)]
    public async Task RecuperarSenha_EmailInvalido_LancaArgumentException(string? email)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RecuperarSenhaAsync(new RecuperarSenhaRequest { Email = email! }));
    }

    [Fact]
    public async Task RedefinirSenha_CodigoValido_TrocaSenhaERevogaSessoes()
    {
        var req = Conta("redefinir_ok@test.com", "11222333000181");
        await _service.CriarContaAsync(req);
        await ConfirmarEmpresaEUsuario(req.Email, Normaliza(req.Cnpj));

        var loginAntigo = await _service.LoginAsync(new LoginRequest
        {
            Email = req.Email,
            Senha = req.Senha
        });
        Assert.NotEmpty(loginAntigo.Token);

        var recuperacao = await _service.RecuperarSenhaAsync(new RecuperarSenhaRequest { Email = req.Email });
        var resultado = await _service.RedefinirSenhaAsync(new RedefinirSenhaRequest
        {
            Email = req.Email,
            Codigo = recuperacao.CodigoResetTeste!,
            NovaSenha = "NovaSenha@456"
        });

        Assert.True(resultado.Sucesso);

        // Senha original deixa de funcionar.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.LoginAsync(new LoginRequest { Email = req.Email, Senha = req.Senha }));

        // Nova senha funciona.
        var loginNovo = await _service.LoginAsync(new LoginRequest
        {
            Email = req.Email,
            Senha = "NovaSenha@456"
        });
        Assert.NotEmpty(loginNovo.Token);

        // Sessao anterior foi revogada.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ValidarTokenAsync(loginAntigo.Token));
    }

    [Fact]
    public async Task RedefinirSenha_CodigoInvalido_LancaUnauthorizedAccessException()
    {
        var req = Conta("redefinir_cod_errado@test.com", "11222333000181");
        await _service.CriarContaAsync(req);
        await ConfirmarEmpresaEUsuario(req.Email, Normaliza(req.Cnpj));

        await _service.RecuperarSenhaAsync(new RecuperarSenhaRequest { Email = req.Email });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.RedefinirSenhaAsync(new RedefinirSenhaRequest
            {
                Email = req.Email,
                Codigo = "000000",
                NovaSenha = "NovaSenha@456"
            }));
    }

    [Fact]
    public async Task RedefinirSenha_CodigoUsadoDuasVezes_SegundaFalha()
    {
        var req = Conta("redefinir_reuso@test.com", "11222333000181");
        await _service.CriarContaAsync(req);
        await ConfirmarEmpresaEUsuario(req.Email, Normaliza(req.Cnpj));

        var recuperacao = await _service.RecuperarSenhaAsync(new RecuperarSenhaRequest { Email = req.Email });
        var redefinir = new RedefinirSenhaRequest
        {
            Email = req.Email,
            Codigo = recuperacao.CodigoResetTeste!,
            NovaSenha = "NovaSenha@456"
        };

        await _service.RedefinirSenhaAsync(redefinir);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.RedefinirSenhaAsync(redefinir));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")]
    public async Task RedefinirSenha_NovaSenhaCurta_LancaArgumentException(string novaSenha)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RedefinirSenhaAsync(new RedefinirSenhaRequest
            {
                Email = "qualquer@test.com",
                Codigo = "123456",
                NovaSenha = novaSenha
            }));
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // VALIDAR TOKEN
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task ValidarToken_Valido_RetornaSessaoCorreta()
    {
        var req = Conta("token_ok@test.com", "11222333000181");
        await _service.CriarContaAsync(req);
        await ConfirmarEmpresaEUsuario(req.Email, Normaliza(req.Cnpj));

        var login = await _service.LoginAsync(new LoginRequest
        {
            Email = req.Email,
            Senha = req.Senha
        });

        var sessao = await _service.ValidarTokenAsync(login.Token);

        Assert.Equal(login.Usuario.Id, sessao.Usuario.Id);
        Assert.Equal(login.Empresa.Cnpj, sessao.Empresa.Cnpj);
    }

    [Fact]
    public async Task ValidarToken_TokenInvalido_LancaUnauthorizedAccessException()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ValidarTokenAsync("tokenfalsoqualquer"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ValidarToken_TokenVazioOuNulo_LancaUnauthorizedAccessException(string? token)
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ValidarTokenAsync(token!));
    }

    [Fact]
    public async Task ValidarToken_TokenExpirado_LancaUnauthorizedAccessException()
    {
        var req = Conta("expired@test.com", "11222333000181");
        await _service.CriarContaAsync(req);
        await ConfirmarEmpresaEUsuario(req.Email, Normaliza(req.Cnpj));

        var login = await _service.LoginAsync(new LoginRequest
        {
            Email = req.Email,
            Senha = req.Senha
        });

        await ExpirarToken(login.Token);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ValidarTokenAsync(login.Token));
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // UNICIDADE DE EMAIL â€” PROTEÃ‡ÃƒO MULTITENANT
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task EmailUnico_MesmoEmail_DuasEmpresas_SegundaFalha()
    {
        await _service.CriarContaAsync(Conta("global_mt@test.com", "11222333000181"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CriarContaAsync(Conta("global_mt@test.com", "99888777000100")));
    }

    [Fact]
    public async Task EmailUnico_EmailsDiferentes_AmbosPermitidos()
    {
        await _service.CriarContaAsync(Conta("mt_a@test.com", "11222333000181"));
        var result = await _service.CriarContaAsync(Conta("mt_b@test.com", "22333444000181"));

        Assert.NotNull(result);
        Assert.Equal("mt_b@test.com", result.Usuario.Email);
    }

    [Fact]
    public async Task IndiceEmail_GarantirColunas_Idempotente_MultiplasChamadas()
    {
        // TrÃªs instÃ¢ncias diferentes do service chamam GarantirTabelasAsync sequencialmente
        var s1 = Build(_cs);
        var s2 = Build(_cs);
        var s3 = Build(_cs);

        await s1.CriarContaAsync(Conta("seq1@test.com", "11222333000181"));
        await s2.CriarContaAsync(Conta("seq2@test.com", "22333444000181"));
        await s3.CriarContaAsync(Conta("seq3@test.com", "33444555000181"));
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // HELPERS
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static CriarContaRequest Conta(
        string email,
        string cnpj = "11222333000181") => new()
        {
            Cnpj = cnpj,
            Email = email,
            UsuarioNome = "Admin Teste",
            Login = "",
            Senha = "Senha@123",
            Perfil = "Administrador",
            RazaoSocial = "Empresa Teste Ltda",
            NomeFantasia = "Empresa Teste"
        };

    private static string Normaliza(string v) => CnpjUtils.Normalizar(v);

    private async Task ConfirmarEmpresaEUsuario(string email, string cnpj)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync();

        await using var cmd1 = new NpgsqlCommand(
            "UPDATE EMPRESA SET REGISTRADO='S' WHERE CNPJ=@c", conn);
        cmd1.Parameters.AddWithValue("@c", cnpj);
        await cmd1.ExecuteNonQueryAsync();

        await using var cmd2 = new NpgsqlCommand(
            "UPDATE RET_USUARIO SET CONFIRMADO='S' WHERE LOWER(EMAIL)=@e", conn);
        cmd2.Parameters.AddWithValue("@e", email.ToLower());
        await cmd2.ExecuteNonQueryAsync();
    }

    private async Task DesativarUsuario(string email)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE RET_USUARIO SET ATIVO='N' WHERE LOWER(EMAIL)=@e", conn);
        cmd.Parameters.AddWithValue("@e", email.ToLower());
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task AtualizarHashDireto(string email, string senhaHash, string salt)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE RET_USUARIO SET SENHA_HASH=@h, SENHA_SALT=@s WHERE LOWER(EMAIL)=@e", conn);
        cmd.Parameters.AddWithValue("@h", senhaHash);
        cmd.Parameters.AddWithValue("@s", salt);
        cmd.Parameters.AddWithValue("@e", email.ToLower());
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<string> LerHashDireto(string email)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT SENHA_HASH FROM RET_USUARIO WHERE LOWER(EMAIL)=@e LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@e", email.ToLower());
        return (await cmd.ExecuteScalarAsync())?.ToString() ?? string.Empty;
    }

    private async Task ExpirarToken(string token)
    {
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE RET_SESSAO SET EXPIRA_EM = (now() at time zone 'utc') - interval '1 hour' WHERE TOKEN_HASH=@h", conn);
        cmd.Parameters.AddWithValue("@h", hash);
        await cmd.ExecuteNonQueryAsync();
    }
}

