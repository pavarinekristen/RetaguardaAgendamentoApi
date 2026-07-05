# MySQL Migrations

Estrategia oficial da API: scripts SQL versionados.

O WPF continua usando EF Core migrations para o SQLite local. O MySQL da API nao usa EF Core neste momento, porque a API atual trabalha com `MySql.Data` e SQL direto.

## Padrao

- Cada migration deve seguir o nome `NNN_descricao.sql`.
- Scripts devem ser idempotentes quando possivel (`CREATE TABLE IF NOT EXISTS`, `CREATE INDEX` validado previamente quando necessario).
- A tabela de controle fica em `retaguarda_agendamento.SCHEMA_MIGRATION`.
- Antes de aplicar migrations, o runner tenta gerar backup em `backups/mysql`.

## Aplicar no Docker local

```powershell
docker compose -f docker-compose.mysql.yml up -d
.\apply-mysql-migrations.ps1
```

## Aplicar usando mysql.exe local

```powershell
.\apply-mysql-migrations.ps1 -UseLocalMysql -HostName localhost -Port 3308 -MysqlUser root -MysqlPassword "AgendaRoot@2026"
```

## Sem backup automatico

Use apenas se ja houver backup externo:

```powershell
.\apply-mysql-migrations.ps1 -SkipBackup
```

## Nova migration

1. Criar um arquivo novo, por exemplo `002_adicionar_indice_clientes.sql`.
2. Nao alterar migrations antigas ja aplicadas em ambiente compartilhado.
3. Rodar `.\apply-mysql-migrations.ps1`.
4. Validar API com `GET /health` e smoke runner.
