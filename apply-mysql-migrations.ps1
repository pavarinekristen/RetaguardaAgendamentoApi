param(
    [string]$MigrationsPath = "",
    [string]$ContainerName = "agenda-db-mysql",
    [string]$RootUser = "root",
    [string]$RootPassword = "AgendaRoot@2026",
    [string]$RetaguardaDatabase = "retaguarda_agendamento",
    [string]$OperacionalDatabase = "agenda_operacional",
    [switch]$UseLocalMysql,
    [string]$HostName = "localhost",
    [int]$Port = 3308,
    [string]$MysqlUser = "root",
    [string]$MysqlPassword = "AgendaRoot@2026",
    [switch]$SkipBackup
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($MigrationsPath)) {
    $MigrationsPath = Join-Path $root "mysql-migrations"
}

$backupFolder = Join-Path $root "backups\mysql"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Get-MigrationVersion {
    param([string]$FileName)
    if ($FileName -notmatch "^(\d+)_") {
        throw "Nome de migration invalido: $FileName. Use o padrao 001_descricao.sql."
    }

    return $Matches[1]
}

function Get-FileChecksum {
    param([string]$Path)
    return (Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Invoke-MySqlQuery {
    param([string]$Query)

    if ($UseLocalMysql) {
        & mysql "--host=$HostName" "--port=$Port" "--user=$MysqlUser" "--password=$MysqlPassword" "--batch" "--skip-column-names" "--execute=$Query"
    }
    else {
        & docker exec $ContainerName mysql "--user=$RootUser" "--password=$RootPassword" "--batch" "--skip-column-names" "--execute=$Query"
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao executar query MySQL."
    }
}

function Invoke-MySqlFile {
    param([string]$Path)

    if ($UseLocalMysql) {
        Get-Content -Path $Path -Raw | & mysql "--host=$HostName" "--port=$Port" "--user=$MysqlUser" "--password=$MysqlPassword"
    }
    else {
        Get-Content -Path $Path -Raw | & docker exec -i $ContainerName mysql "--user=$RootUser" "--password=$RootPassword"
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao aplicar migration: $Path"
    }
}

function Backup-Databases {
    if ($SkipBackup) {
        Write-Step "Backup ignorado por parametro"
        return
    }

    New-Item -ItemType Directory -Force -Path $backupFolder | Out-Null
    $backupPath = Join-Path $backupFolder ("mysql-before-migrations-{0}.sql" -f (Get-Date -Format "yyyyMMdd-HHmmss"))

    Write-Step "Gerando backup antes das migrations"
    try {
        if ($UseLocalMysql) {
            & mysqldump "--host=$HostName" "--port=$Port" "--user=$MysqlUser" "--password=$MysqlPassword" --databases $RetaguardaDatabase $OperacionalDatabase --result-file=$backupPath
        }
        else {
            $dump = & docker exec $ContainerName mysqldump "--user=$RootUser" "--password=$RootPassword" --databases $RetaguardaDatabase $OperacionalDatabase
            if ($LASTEXITCODE -eq 0) {
                Set-Content -Path $backupPath -Value $dump -Encoding UTF8
            }
        }
    }
    catch {
        Write-Warning "Backup pre-migration falhou. Se este for um servidor novo, isso e esperado."
    }

    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $backupPath)) {
        Write-Warning "Backup nao foi gerado. Continuando porque pode ser um ambiente novo sem schemas existentes."
        return
    }

    Write-Host "Backup: $backupPath"
}

function Ensure-MigrationTable {
    Write-Step "Garantindo tabela de controle"
    Invoke-MySqlQuery @"
CREATE SCHEMA IF NOT EXISTS $RetaguardaDatabase DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS $RetaguardaDatabase.SCHEMA_MIGRATION (
  ID INT UNSIGNED NOT NULL AUTO_INCREMENT,
  VERSION VARCHAR(30) NOT NULL,
  FILE_NAME VARCHAR(255) NOT NULL,
  CHECKSUM_SHA256 VARCHAR(64) NOT NULL,
  APPLIED_AT DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (ID),
  UNIQUE KEY UK_SCHEMA_MIGRATION_VERSION (VERSION),
  UNIQUE KEY UK_SCHEMA_MIGRATION_FILE (FILE_NAME)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
"@
}

function Test-MigrationApplied {
    param([string]$Version)
    $result = Invoke-MySqlQuery "SELECT COUNT(*) FROM $RetaguardaDatabase.SCHEMA_MIGRATION WHERE VERSION = '$Version';"
    return (($result | Select-Object -First 1) -as [int]) -gt 0
}

function Register-Migration {
    param(
        [string]$Version,
        [string]$FileName,
        [string]$Checksum
    )

    $safeFileName = $FileName.Replace("'", "''")
    Invoke-MySqlQuery "INSERT INTO $RetaguardaDatabase.SCHEMA_MIGRATION (VERSION, FILE_NAME, CHECKSUM_SHA256) VALUES ('$Version', '$safeFileName', '$Checksum');"
}

if (-not (Test-Path $MigrationsPath)) {
    throw "Pasta de migrations nao encontrada: $MigrationsPath"
}

$migrations = Get-ChildItem -Path $MigrationsPath -Filter "*.sql" |
    Sort-Object Name

if ($migrations.Count -eq 0) {
    throw "Nenhuma migration .sql encontrada em: $MigrationsPath"
}

Backup-Databases
Ensure-MigrationTable

foreach ($migration in $migrations) {
    $version = Get-MigrationVersion $migration.Name
    $checksum = Get-FileChecksum $migration.FullName

    if (Test-MigrationApplied $version) {
        Write-Step "Migration $($migration.Name) ja aplicada"
        continue
    }

    Write-Step "Aplicando migration $($migration.Name)"
    Invoke-MySqlFile $migration.FullName
    Register-Migration -Version $version -FileName $migration.Name -Checksum $checksum
}

Write-Step "Migrations MySQL concluidas"
