# Operacao e Checklists

Atualizado em: 07/07/2026.

## Rodar tudo local

### 1. MySQL

```powershell
cd C:\Users\krist\RetaguardaAgendamentoAPI
docker compose -f docker-compose.mysql.yml up -d
```

### 2. API

```powershell
cd C:\Users\krist\RetaguardaAgendamentoAPI
dotnet run --project RetaguardaAgendamentoAPI\RetaguardaAgendamentoAPI.csproj --urls http://localhost:5000
```

### 3. Front web

```powershell
cd C:\Users\krist\RetaguardaAgendamentoAPI\web-laudos
python -m http.server 8080 --bind 127.0.0.1
```

Abrir:

```text
http://127.0.0.1:8080/
```

### 4. WPF

```powershell
cd C:\Users\krist\AgendamentoWpfApp
dotnet run --project AgendamentoWpfApp.csproj
```

## Validar API

Sem token:

```powershell
Invoke-WebRequest -Uri http://localhost:5000/auth/me -UseBasicParsing
```

Resultado esperado:

```text
401
```

Health:

```powershell
Invoke-WebRequest -Uri http://localhost:5000/health -UseBasicParsing
```

Resultados:

- 200: API e banco OK.
- 503: API viva, mas banco degradado/indisponivel.

## Validar front

```powershell
Invoke-WebRequest -Uri http://127.0.0.1:8080/ -UseBasicParsing
```

Resultado esperado:

```text
200
```

## Build

API:

```powershell
dotnet build C:\Users\krist\RetaguardaAgendamentoAPI\RetaguardaAgendamentoAPI.sln
```

WPF:

```powershell
dotnet build C:\Users\krist\AgendamentoWpfApp\AgendamentoWpfApp.sln --ignore-failed-sources
```

Front:

```powershell
node --check C:\Users\krist\RetaguardaAgendamentoAPI\web-laudos\app.js
```

## Testes

API:

```powershell
dotnet test C:\Users\krist\RetaguardaAgendamentoAPI\RetaguardaAgendamentoAPI.sln
```

Observacao:

- Precisa Docker Desktop ativo.

WPF:

```powershell
dotnet run --project C:\Users\krist\AgendamentoWpfApp\AgendamentoWpfApp.Tests\AgendamentoWpfApp.Tests.csproj
```

Smoke WPF:

```powershell
cd C:\Users\krist\RetaguardaAgendamentoAPI
powershell -ExecutionPolicy Bypass -File .\smoke-runner.ps1 -WpfOnly
```

## Checklist antes de publicar front na Hostinger

- API publicada e respondendo em HTTPS.
- `api.clinicaideia.com.br/health` responde.
- `auth/me` sem token retorna 401.
- Login testado.
- CORS inclui `https://laudos.clinicaideia.com.br`.
- `web-laudos/config.js` aponta para API publica.
- Arquivos enviados para `public_html/laudos`.
- Browser/celular testa login e pesquisa.

## Checklist antes de publicar API

- VPS pronta.
- DNS `api.clinicaideia.com.br` apontado.
- MySQL instalado.
- Migrations aplicadas.
- Secrets configurados fora do Git.
- Nginx configurado.
- SSL emitido.
- systemd ativo.
- Backup configurado.
- Logs conferidos.

## Checklist antes de cancelar Linode

- Confirmar que sistema antigo nao esta mais em uso.
- Confirmar onde esta o banco antigo.
- Fazer backup completo.
- Confirmar que nova API esta em producao.
- Confirmar que front novo funciona.
- Confirmar que desktop sincroniza com nova API.
- Manter periodo de convivencia se possivel.

