# API e Backend

Atualizado em: 07/07/2026.

## Projeto

Path:

```text
C:\Users\krist\RetaguardaAgendamentoAPI\RetaguardaAgendamentoAPI
```

Solution:

```text
C:\Users\krist\RetaguardaAgendamentoAPI\RetaguardaAgendamentoAPI.sln
```

Stack:

- ASP.NET Core .NET 8.
- MySQL.
- SQL direto com `MySql.Data`.
- Controllers + Services + Models.

## Configuracao

Arquivo principal:

```text
RetaguardaAgendamentoAPI\appsettings.json
```

Configuracoes importantes:

- `ConnectionStrings:DefaultConnection`
- `ConnectionStrings:AdminConnection`
- `AgendaOperacionalDatabase`
- `Cors:AllowedOrigins`
- `Email:*`
- `RateLimiting:*`

Em producao:

- Nao colocar senha real no arquivo versionado.
- Usar variaveis de ambiente ou arquivo seguro do servidor.
- Configurar CORS para:

```text
https://laudos.clinicaideia.com.br
https://api.clinicaideia.com.br
```

## Endpoints atuais

Autenticacao:

```text
POST /auth/criar-conta
POST /auth/login
POST /auth/confirmar-email
POST /auth/reenviar-confirmacao
POST /auth/recuperar-senha
POST /auth/redefinir-senha
GET  /auth/me
```

Sincronizacao:

```text
POST /sincroniza/agenda/snapshot
```

Portal web:

```text
GET /portal/clientes?nome=&id=&empresa=&limite=50
GET /portal/clientes/{idLocal}
```

Health:

```text
GET /health
```

Updates:

```text
Controllers/Updates/SparkCoreUpdateController.cs
```

## Portal clientes

Arquivos:

```text
Controllers/Portal/PortalClientesController.cs
Services/Portal/PortalClienteService.cs
Models/Portal/PortalClienteModels.cs
```

Regras:

- Exige Bearer token.
- Valida token via `AuthService`.
- Filtra sempre por `ID_EMPRESA` da sessao.
- Pesquisa por:
  - nome
  - ID sequencial visivel
  - empresa
- Devolve `IdCadastro` para exibicao.
- Mantem `IdLocal` apenas como identificador tecnico para detalhe.

## Como rodar local

Subir MySQL:

```powershell
cd C:\Users\krist\RetaguardaAgendamentoAPI
docker compose -f docker-compose.mysql.yml up -d
```

Subir API:

```powershell
cd C:\Users\krist\RetaguardaAgendamentoAPI
dotnet run --project RetaguardaAgendamentoAPI\RetaguardaAgendamentoAPI.csproj --urls http://localhost:5000
```

Validar API:

```powershell
Invoke-WebRequest -Uri http://localhost:5000/auth/me -UseBasicParsing
```

Resultado esperado sem token:

```text
HTTP 401
```

Validar health:

```powershell
Invoke-WebRequest -Uri http://localhost:5000/health -UseBasicParsing
```

Observacao:

- `/health` retorna 503 se a API estiver viva, mas o MySQL indisponivel.
- Isso significa `DEGRADED`, nao necessariamente API fora do ar.

## Build e testes

Build:

```powershell
dotnet build RetaguardaAgendamentoAPI.sln
```

Testes:

```powershell
dotnet test RetaguardaAgendamentoAPI.sln
```

Observacao:

- Testes de integracao usam Docker/Testcontainers.
- Se Docker Desktop estiver fechado, parte dos testes falha antes de testar a regra.

## Pendencias tecnicas antes do deploy

Obrigatorio antes de producao:

- Configurar secrets fora do codigo.
- Definir URL final da API.
- Configurar HTTPS.
- Configurar reverse proxy.
- Ajustar forwarded headers.
- Criar rotina de backup.
- Definir logs e retencao.
- Revisar snapshot dinamico com whitelist.

Recomendado:

- Implementar logout.
- Limpar sessoes expiradas.
- Padronizar middleware de erro.
- Swagger apenas em desenvolvimento.
- Migrar de `MySql.Data` para `MySqlConnector` futuramente.

