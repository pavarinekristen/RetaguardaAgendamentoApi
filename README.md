# RetaguardaAgendamentoAPI

API base para o sistema de agendamento offline first.

## Estado atual em 03/07/2026

O projeto agora tem duas frentes funcionando em ambiente local:

- API de retaguarda com autenticacao, confirmacao de e-mail, login, token, sessao e snapshot base de sincronizacao.
- WPF com login/criacao de conta conectados na API, SQLite local com EF Core migrations e tela pos-login inicial para cadastro e pesquisa de clientes.

Importante: a migration SQLite do WPF nao altera o MySQL automaticamente. A ligacao com a API acontece pela sincronizacao: o WPF le o SQLite local, monta um snapshot e envia para `/sincroniza/agenda/snapshot`; a API grava/adapta os dados no MySQL operacional.

## Marco validado em 02/07/2026

O fluxo principal de autenticacao ja foi validado de ponta a ponta:

- Criacao de conta pela API.
- Gravacao real no MySQL.
- Envio real do codigo de confirmacao por Gmail SMTP.
- Confirmacao do e-mail com codigo recebido.
- Liberacao da empresa e do usuario.
- Login com geracao de token de acesso.
- Criacao de sessao em banco.
- Validacao do token em `/auth/me`.

Conferencia feita diretamente no banco `retaguarda_agendamento`:

- `EMPRESA`: 1 registro.
- `RET_USUARIO`: 3 usuarios.
- `RET_EMAIL_TOKEN`: 2 tokens de confirmacao.
- `RET_SESSAO`: 3 sessoes.

Exemplo validado:

- Usuario `kristenpp2003@gmail.com` criado, confirmado e com login realizado.
- Usuario `devkristenpp@gmail.com` criado, confirmado e com login realizado.
- Empresa vinculada: `Clinica Exemplo`.
- CNPJ vinculado: `11222333000181`.

Isso confirma que a base da API para conta, autenticacao, confirmacao por e-mail e token ja esta funcionando.

## Mantido da retaguarda original

- Criacao de conta, login, sessao/token e validacao de token.
- Cadastro administrativo minimo de empresa e usuario.
- Sincronizacao por snapshot, renomeada para agenda.
- Utilitario de validacao de CNPJ.
- Modelo padrao de retorno de erro.

## Removido

- PDV, NFC-e/NFe, ACBr, fiscal, produtos, imagens de produto e endpoints de teste.
- Credenciais reais de banco/e-mail.
- Sincronizacao antiga por upload de SQLite em base64.

## Rotas iniciais

- `POST /auth/criar-conta`
- `POST /auth/login`
- `POST /auth/confirmar-email`
- `POST /auth/reenviar-confirmacao`
- `POST /auth/recuperar-senha`
- `GET /auth/me`
- `POST /sincroniza/agenda/snapshot`

## Smoke runner do sistema

Foi adicionado um runner local para validar o sistema inteiro em duas partes:

- WPF: compila o app e executa um smoke automatico da persistencia local de clientes.
- API: compila a solucao, checa Docker, sobe MySQL, inicia a API sem envio real de e-mail, cria conta, confirma e-mail, faz login, valida `/auth/me` e envia snapshot minimo.

```powershell
.\smoke-runner.ps1 -SkipDotnetTests
```

Para rodar somente o smoke do WPF, sem depender do Docker:

```powershell
.\smoke-runner.ps1 -WpfOnly
```

Para rodar somente o smoke da API:

```powershell
.\smoke-runner.ps1 -ApiOnly -SkipDotnetTests
```

Para rodar tambem os testes automatizados:

```powershell
.\smoke-runner.ps1
```

Observacao: o fluxo completo da API depende do Docker Desktop ativo. O smoke do WPF nao abre a interface grafica; ele testa migrations SQLite e persistencia local usando o projeto `AgendamentoWpfApp.Smoke`.

## WPF pos-login

O `DashboardWindow` do projeto WPF agora abre uma area interna de clientes apos o login.

Funcionalidades iniciais:

- cadastro de cliente com nome, CPF, e-mail, telefone, nascimento, endereco, bairro e cidade;
- controle basico de atendimento com dia, horario, local, status e observacoes;
- pesquisa por nome, CPF, e-mail, telefone, local ou status;
- grade de clientes e painel de detalhe;
- armazenamento local em SQLite em `%LOCALAPPDATA%\RetaguardaAgendamento\agenda.sqlite`;
- botao `Sincronizar` para enviar snapshot do SQLite para a API.

Verificacao atual:

- Cadastro confirmado no SQLite local em `03/07/2026`.
- Tabela `CLIENTES`: 1 registro.
- Cliente conferido: `Kristen`, CPF `12345678910`, telefone `34992902032`, horario `03/07/2026 15:00`, local `ideia`.
- MySQL `agenda_operacional`: ainda sem tabelas, indicando que esse cadastro local ainda nao foi sincronizado para a API.

## Modulos laterais previstos

- Clientes: cadastro, edicao, pesquisa, historico, dados de contato e controle de atendimento.
- Agenda: calendario, horarios por dia, novo agendamento, status, local/sala/profissional e conflitos.
- Servicos: cadastro dos tipos de servico, duracao, valor, descricao e vinculo com agenda/laudo.
- Profissionais/salas: cadastro de profissionais, salas/local de atendimento, disponibilidade e agenda vinculada.
- Laudos: gerar PDF a partir de modelo existente, preenchendo somente lacunas com dados cadastrados.
- Sincronizacao: enviar dados locais SQLite para API/MySQL e depois receber dados remotos quando houver sincronizacao bidirecional.
- Configuracoes: dados da empresa, preferencias, modelo de laudo, parametros locais e conexao com API.

## Proximos passos anotados

1. Adicionar mascaras e validacoes nos campos do WPF:
   - CPF;
   - telefone;
   - e-mail;
   - data;
   - horario;
   - campos obrigatorios.
2. Adicionar marca d'agua fixa no canto inferior direito:
   - texto: `Feito com ❤️ pela Sparkware`;
   - aparecer no login;
   - aparecer no sistema apos login.
3. Evoluir os modulos laterais para telas reais, comecando por Agenda, Servicos e Profissionais/salas.
4. Preparar geracao de PDF de laudo:
   - usar o modelo existente;
   - preencher somente as lacunas com dados cadastrados;
   - nao reescrever o documento inteiro;
   - liberar o PDF para download/salvar.
5. Ligar o laudo aos dados de cliente, atendimento, servico, profissional/local e observacoes.
6. Melhorar sincronizacao:
   - garantir botao/fluxo claro de envio para API;
   - confirmar gravacao no MySQL `agenda_operacional`;
   - registrar status de sincronizacao no SQLite.

## Migrations e banco de dados

Ja existe migration no WPF para o SQLite local.

WPF:

- Usa EF Core + SQLite.
- DbContext: `Data/AgendaDbContext.cs`.
- Banco local padrao: `%LOCALAPPDATA%\RetaguardaAgendamento\agenda.sqlite`.
- Migration inicial: `Migrations/20260703120000_InitialAgendaSchema.cs`.
- Tabela inicial criada: `CLIENTES`.
- Campos de sincronizacao local: `IdLocal`, `AtualizadoEm`, `SincronizadoEm`, `Excluido`, `HashSincronizacao`.

API:

- Ainda nao usa EF Core migrations.
- Usa `MySql.Data` e comandos SQL diretos.
- A criacao inicial do banco local fica em `mysql-init/retaguarda-agendamento.sql`.
- Alguns services tambem possuem metodos `GarantirTabelasAsync` / `GarantirEstruturaAsync` para criar ou ajustar tabelas quando a API executa.
- O endpoint `/sincroniza/agenda/snapshot` recebe o snapshot vindo do SQLite e cria/atualiza tabelas no banco `agenda_operacional`.

Como a ligacao funciona:

1. Uma migration altera o schema local do SQLite no WPF.
2. O WPF salva dados nesse schema local.
3. O usuario ou o sistema executa sincronizacao.
4. O WPF monta um snapshot das tabelas do SQLite.
5. A API recebe o snapshot e grava/adapta os dados no MySQL operacional.

Portanto, migration SQLite nao executa no MySQL diretamente. O MySQL acompanha os dados/schema operacional por meio do contrato de sincronizacao.

Para escalar melhor, a recomendacao e escolher um modelo de evolucao para o MySQL antes de crescer os modulos:

- manter scripts SQL versionados por release; ou
- adotar EF Core com migrations; ou
- usar uma ferramenta propria de migration SQL, como DbUp/Flyway/Liquibase.

O ponto importante e nao misturar varios caminhos ao mesmo tempo. Hoje o WPF esta no caminho EF Core migrations + SQLite, e a API esta no caminho SQL direto + script inicial + estrutura dinamica do snapshot.

## Estrutura atual

API:

```text
RetaguardaAgendamentoAPI/
  Controllers/
    AuthController.cs
    Sincronizacao/AgendaSnapshotController.cs
  Models/
    AuthModels.cs
    Sincronizacao/AgendaSnapshotModels.cs
    Transiente/RetornoJsonErro.cs
  Services/
    AuthService.cs
    Email/EmailService.cs
    Sincronizacao/AgendaSnapshotService.cs
  Util/
    CnpjUtils.cs
  Program.cs
  Startup.cs
```

Testes:

```text
RetaguardaAgendamentoAPI.Tests/
  Auth/AuthServiceTests.cs
  Util/CnpjUtilsTests.cs
```

WPF:

```text
C:\Users\krist\AgendamentoWpfApp/
  MainWindow.xaml              login
  CreateAccountWindow.xaml     criar conta e confirmar e-mail
  DashboardWindow.xaml         pos-login e clientes
  Models/Cliente.cs            entidade local SQLite
  Data/AgendaDbContext.cs      DbContext local
  Migrations/                  migrations EF Core SQLite
  ClienteLocalStore.cs         persistencia local em SQLite
  Services/AgendaSnapshotSyncService.cs sincronizacao com a API
  SessionState.cs              sessao do usuario logado
  AppSettings.cs               configuracao da URL da API
  AgendamentoWpfApp.Smoke/     smoke automatico do WPF
```

Essa estrutura funciona para a fase atual, mas ainda e simples. A API esta organizada por Controllers, Models e Services dentro de um unico projeto. O WPF ainda esta com telas e logica no code-behind.

## Direcao recomendada para escalar

O modelo recomendado para crescer o projeto e uma arquitetura em camadas, sem quebrar o sistema em microservicos agora. O melhor caminho aqui e um monolito modular bem organizado:

```text
src/
  RetaguardaAgendamento.Api/
    Controllers/
    Program.cs
    Startup.cs

  RetaguardaAgendamento.Application/
    Auth/
    Clientes/
    Agenda/
    Servicos/
    Profissionais/
    Sincronizacao/

  RetaguardaAgendamento.Domain/
    Empresas/
    Usuarios/
    Clientes/
    Agenda/
    Servicos/
    Profissionais/

  RetaguardaAgendamento.Infrastructure/
    MySql/
    Email/
    Sync/
    Security/

  RetaguardaAgendamento.Contracts/
    Auth/
    Clientes/
    Agenda/
    Sincronizacao/

tests/
  RetaguardaAgendamento.Tests/
```

Para o WPF, a direcao recomendada e MVVM:

```text
AgendamentoWpfApp/
  Views/
    LoginView.xaml
    ClientesView.xaml
    AgendaView.xaml
  ViewModels/
    LoginViewModel.cs
    ClientesViewModel.cs
    AgendaViewModel.cs
  Models/
    ClienteLocal.cs
    AgendamentoLocal.cs
  Services/
    ApiClient.cs
    ClienteLocalStore.cs
    SyncService.cs
  Data/
    SQLite/
```

Ordem recomendada de evolucao:

1. Separar contratos de request/response da API.
2. Criar modulo `Clientes` real na API e no WPF.
3. Evoluir a sincronizacao offline first com controle de conflito.
4. Criar endpoints/contratos mais especificos para Clientes, Agenda, Servicos e Profissionais.
5. Separar gradualmente `Application`, `Domain` e `Infrastructure` quando a regra de negocio crescer.
6. Definir estrategia unica de migrations/schema do MySQL antes do deploy.

## Confirmacao de e-mail

Ao criar conta, a API gera um codigo de confirmacao de 6 digitos e grava somente o hash no banco.

O envio real por Gmail SMTP foi configurado e testado com sucesso.

Configuracao local em `RetaguardaAgendamentoAPI/appsettings.Development.json`:

```json
"Email": {
  "Enabled": true,
  "Host": "smtp.gmail.com",
  "Port": 587,
  "EnableSsl": true,
  "From": "retaguardaagendamento@gmail.com",
  "DisplayName": "Retaguarda Agendamento",
  "Username": "retaguardaagendamento@gmail.com",
  "Password": "senha de app do Google configurada",
  "ReturnConfirmationCodeInResponse": false
}
```

Observacoes:

- `retaguardaagendamento@gmail.com` e o remetente fixo do sistema.
- O e-mail informado no cadastro e o destinatario e pode ser qualquer e-mail valido.
- A senha usada e uma senha de app do Google, nao a senha normal do Gmail.
- Com `ReturnConfirmationCodeInResponse=false`, o codigo nao aparece no JSON; ele chega somente no e-mail.

Para configurar outra conta Gmail:

- ativar verificacao em duas etapas na conta Google;
- gerar uma senha de app;
- colocar a senha de app em `Email:Password`;
- mudar `Email:Enabled` para `true`;
- reiniciar a API.

Endpoints:

- `POST /auth/confirmar-email`
- `POST /auth/reenviar-confirmacao`

Fluxo testado no Postman:

1. Definir `emailUsuario` no environment do Postman.
2. Chamar `Auth -> Criar conta`.
3. Copiar o codigo recebido no e-mail.
4. Chamar `Auth -> Confirmar email` com o codigo.
5. Chamar `Auth -> Login`.
6. O login retorna o token de acesso.

Body de confirmacao:

```json
{
  "email": "{{emailUsuario}}",
  "codigo": "CODIGO_RECEBIDO_NO_EMAIL"
}
```

## MySQL local

O banco local usa Docker Compose separado da API:

```powershell
docker compose -f docker-compose.mysql.yml up -d
```

O MySQL fica exposto na porta local `3308`, para evitar conflito com outros bancos nas portas `3306` e `3307`.

Conexoes configuradas em `RetaguardaAgendamentoAPI/appsettings.Development.json`:

- `retaguarda_agendamento`: empresa, usuario e sessao.
- `agenda_operacional`: dados sincronizados pelo snapshot.

O script inicial fica em `mysql-init/retaguarda-agendamento.sql` e roda automaticamente quando o volume do MySQL ainda esta vazio.

Para recriar o banco do zero em ambiente local:

```powershell
docker compose -f docker-compose.mysql.yml down -v
docker compose -f docker-compose.mysql.yml up -d
```

Atencao: `down -v` apaga os dados locais do banco.

## Migrations MySQL versionadas

A estrategia oficial da API agora e manter scripts SQL versionados em `mysql-migrations/`.

O WPF continua com EF Core migrations para o SQLite local. A API continua com `MySql.Data` e SQL direto, por isso o MySQL fica no caminho de scripts SQL versionados em vez de EF Core migrations.

Arquivos principais:

- `mysql-migrations/001_baseline_schema.sql`: schema inicial versionado.
- `mysql-migrations/README.md`: regras de criacao e aplicacao de migrations.
- `apply-mysql-migrations.ps1`: runner que aplica apenas scripts ainda nao registrados.
- `retaguarda_agendamento.SCHEMA_MIGRATION`: tabela de controle das migrations aplicadas.

Aplicar no Docker local:

```powershell
docker compose -f docker-compose.mysql.yml up -d
.\apply-mysql-migrations.ps1
```

Aplicar usando `mysql.exe` local:

```powershell
.\apply-mysql-migrations.ps1 -UseLocalMysql -HostName localhost -Port 3308 -MysqlUser root -MysqlPassword "AgendaRoot@2026"
```

O runner tenta criar backup antes de aplicar migrations em `backups/mysql`. Em servidor novo, se os bancos ainda nao existirem, o backup pode ser ignorado com aviso e a baseline cria os schemas.
