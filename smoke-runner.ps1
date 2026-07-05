param(
    [string]$ApiUrl = "http://localhost:5000",
    [string]$WpfRoot = "C:\Users\krist\AgendamentoWpfApp",
    [switch]$Restore,
    [switch]$SkipDotnetTests,
    [switch]$KeepApiRunning,
    [switch]$ApiOnly,
    [switch]$WpfOnly
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-Checked {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Comando falhou ($LASTEXITCODE): $FilePath $($Arguments -join ' ')"
    }
}

function Invoke-Json {
    param(
        [string]$Method,
        [string]$Url,
        [object]$Body = $null,
        [string]$Token = ""
    )

    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        $headers["Authorization"] = "Bearer $Token"
    }

    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $Url -Headers $headers
    }

    $json = $Body | ConvertTo-Json -Depth 20
    return Invoke-RestMethod -Method $Method -Uri $Url -Headers $headers -ContentType "application/json" -Body $json
}

function Test-DockerAvailable {
    try {
        $oldPreference = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        & docker ps > $null 2>&1
        $code = $LASTEXITCODE
        $ErrorActionPreference = $oldPreference
        return $code -eq 0
    }
    catch {
        return $false
    }
    finally {
        if ($null -ne $oldPreference) {
            $ErrorActionPreference = $oldPreference
        }
    }
}

function Wait-HttpReady {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 45
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try {
            Invoke-RestMethod -Method Get -Uri "$Url/auth/me" | Out-Null
        }
        catch {
            if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 401) {
                return
            }
        }

        Start-Sleep -Milliseconds 800
    } while ((Get-Date) -lt $deadline)

    throw "API nao ficou pronta em $TimeoutSeconds segundos: $Url"
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

if (-not $ApiOnly) {
    Write-Step "Compilando WPF"
    Invoke-Checked "dotnet" @("build", "$WpfRoot\AgendamentoWpfApp.csproj", "--ignore-failed-sources")

    Write-Step "Compilando smoke WPF"
    Invoke-Checked "dotnet" @("build", "$WpfRoot\AgendamentoWpfApp.Smoke\AgendamentoWpfApp.Smoke.csproj", "--ignore-failed-sources")

    Write-Step "Rodando smoke WPF"
    Invoke-Checked "dotnet" @("run", "--project", "$WpfRoot\AgendamentoWpfApp.Smoke\AgendamentoWpfApp.Smoke.csproj", "--no-build")
}

if ($WpfOnly) {
    Write-Step "Smoke de sistema finalizado apenas com WPF"
    return
}

Write-Step "Compilando solucao"
if ($Restore) {
    Invoke-Checked "dotnet" @("restore", "RetaguardaAgendamentoAPI.sln", "--ignore-failed-sources")
}

Invoke-Checked "dotnet" @("build", "RetaguardaAgendamentoAPI.sln", "--ignore-failed-sources")

if (-not $SkipDotnetTests) {
    Write-Step "Rodando testes automatizados"
    Invoke-Checked "dotnet" @("test", "RetaguardaAgendamentoAPI.sln", "--no-build")
}

Write-Step "Checando Docker"
if (-not (Test-DockerAvailable)) {
    throw "Docker nao esta disponivel. Abra o Docker Desktop e rode novamente o smoke runner."
}

Write-Step "Subindo MySQL local"
Invoke-Checked "docker" @("compose", "-f", "docker-compose.mysql.yml", "up", "-d")

Write-Step "Iniciando API em modo smoke"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Email__Enabled = "false"
$env:Email__ReturnConfirmationCodeInResponse = "true"

$apiProcess = Start-Process `
    -FilePath "dotnet" `
    -ArgumentList @("run", "--no-restore", "--project", "RetaguardaAgendamentoAPI", "--urls", $ApiUrl) `
    -WorkingDirectory $root `
    -PassThru `
    -WindowStyle Hidden

try {
    Wait-HttpReady -Url $ApiUrl

    $stamp = Get-Date -Format "yyyyMMddHHmmss"
    $email = "smoke.$stamp@example.com"
    $cnpj = "11222333000181"
    $senha = "Senha@123"

    Write-Step "Criando conta smoke"
    $criarConta = Invoke-Json -Method Post -Url "$ApiUrl/auth/criar-conta" -Body @{
        cnpj = $cnpj
        razaoSocial = "Clinica Smoke Ltda"
        nomeFantasia = "Clinica Smoke"
        email = $email
        usuarioNome = "Usuario Smoke"
        login = ""
        senha = $senha
        perfil = "Administrador"
    }

    if ([string]::IsNullOrWhiteSpace($criarConta.codigoConfirmacaoTeste)) {
        throw "A API nao retornou codigoConfirmacaoTeste. Verifique Email__ReturnConfirmationCodeInResponse."
    }

    Write-Step "Confirmando e-mail"
    Invoke-Json -Method Post -Url "$ApiUrl/auth/confirmar-email" -Body @{
        email = $email
        codigo = $criarConta.codigoConfirmacaoTeste
    } | Out-Null

    Write-Step "Fazendo login"
    $login = Invoke-Json -Method Post -Url "$ApiUrl/auth/login" -Body @{
        email = $email
        senha = $senha
    }

    if ([string]::IsNullOrWhiteSpace($login.token)) {
        throw "Login nao retornou token."
    }

    Write-Step "Validando /auth/me"
    $me = Invoke-Json -Method Get -Url "$ApiUrl/auth/me" -Token $login.token
    if ($me.usuario.email -ne $email) {
        throw "Auth/me retornou usuario inesperado."
    }

    Write-Step "Enviando snapshot minimo"
    $snapshot = Invoke-Json -Method Post -Url "$ApiUrl/sincroniza/agenda/snapshot" -Token $login.token -Body @{
        dispositivoId = "SMOKE-RUNNER"
        tabelas = @(
            @{
                nome = "CLIENTES"
                registros = @(
                    @{
                        idLocal = "cliente-smoke-001"
                        hash = "cliente-smoke-001-v1"
                        dadosJson = '{"Nome":"Cliente Smoke","Telefone":"11999999999","Cpf":"12345678909","Email":"cliente.smoke@example.com"}'
                    }
                )
            },
            @{
                nome = "AGENDAMENTOS"
                registros = @(
                    @{
                        idLocal = "agenda-smoke-001"
                        hash = "agenda-smoke-001-v1"
                        dadosJson = '{"ClienteIdLocal":"cliente-smoke-001","Data":"2026-07-10","Horario":"14:00","Local":"Sala 1","Status":"Marcado"}'
                    }
                )
            }
        )
    }

    Write-Step "Smoke finalizado com sucesso"
    Write-Host "Usuario: $email"
    Write-Host "Banco operacional: $($snapshot.bancoOperacional)"
    Write-Host "Registros sincronizados: $($snapshot.totalRegistros)"
}
finally {
    if (-not $KeepApiRunning -and $apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force
    }
}
