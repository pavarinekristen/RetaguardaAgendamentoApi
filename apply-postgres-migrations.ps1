# Aplica as migrations Postgres (postgres-migrations/*.sql) no container local agenda-db-postgres.
# Uso:  .\apply-postgres-migrations.ps1
# Requer o Postgres local no ar:  docker compose -f docker-compose.postgres.yml up -d

$ErrorActionPreference = "Stop"
$container = "agenda-db-postgres"
$database  = "retaguarda_agendamento"
$migrationsDir = Join-Path $PSScriptRoot "postgres-migrations"

# Confere se o container esta rodando
$running = docker ps --filter "name=$container" --format "{{.Names}}"
if ($running -ne $container) {
    Write-Error "Container '$container' nao esta rodando. Rode: docker compose -f docker-compose.postgres.yml up -d"
}

$arquivos = Get-ChildItem -Path $migrationsDir -Filter "*.sql" | Sort-Object Name
foreach ($arq in $arquivos) {
    Write-Host "==> Aplicando $($arq.Name)" -ForegroundColor Cyan
    docker cp $arq.FullName "${container}:/tmp/$($arq.Name)"
    docker exec $container psql -U postgres -d $database -v ON_ERROR_STOP=1 -f "/tmp/$($arq.Name)"
    if ($LASTEXITCODE -ne 0) { Write-Error "Falha ao aplicar $($arq.Name)" }
}

Write-Host "Migrations aplicadas com sucesso." -ForegroundColor Green
