# Banco de Dados e Sincronizacao

Atualizado em: 07/07/2026.

## Bancos usados

### SQLite local

Usado pelo WPF.

Path:

```text
%LOCALAPPDATA%\RetaguardaAgendamento\agenda.sqlite
```

Responsavel por:

- Operacao offline/local.
- Cadastro.
- Agenda.
- Laudos.
- Historico.
- Configuracoes locais.

### MySQL administrativo

Banco:

```text
retaguarda_agendamento
```

Responsavel por:

- Empresas.
- Usuarios.
- Sessoes.
- Tokens de email.
- Controle de migrations.

### MySQL operacional

Banco:

```text
agenda_operacional
```

Responsavel por:

- Dados sincronizados do WPF.
- Tabelas finais do snapshot.
- Auditoria.
- Outbox.
- Controle de dispositivos.
- Execucoes de sincronizacao.

## MySQL local via Docker

Arquivo:

```text
docker-compose.mysql.yml
```

Container:

```text
agenda-db-mysql
```

Porta:

```text
localhost:3308 -> container:3306
```

Subir:

```powershell
docker compose -f docker-compose.mysql.yml up -d
```

Verificar:

```powershell
docker ps
```

## Migrations MySQL

Pasta:

```text
mysql-migrations
```

Arquivos:

```text
001_baseline_schema.sql
002_create_agenda_admin_user.sql
README.md
```

Runner:

```text
apply-mysql-migrations.ps1
```

Aplicar local:

```powershell
.\apply-mysql-migrations.ps1
```

## Como a sincronizacao funciona

1. WPF grava dados no SQLite.
2. WPF monta snapshot das tabelas locais.
3. WPF envia para:

```text
POST /sincroniza/agenda/snapshot
```

4. API valida token.
5. API identifica empresa.
6. API grava no MySQL operacional.
7. Portal web consulta o MySQL operacional.

## Estado atual do snapshot

Pontos fortes:

- Funciona genericamente.
- Cria/atualiza tabelas finais conforme payload.
- Mantem `ID_EMPRESA`.
- Mantem `ID_LOCAL`.
- Marca ausentes como excluidos.
- Registra auditoria/outbox.

Pontos fracos:

- Ainda aceita tabelas/colunas dinamicas.
- Precisa de whitelist.
- Precisa de limites por payload.
- Precisa reduzir volume de auditoria/outbox em syncs iguais.

## Banco atual da cliente

Cenario mais provavel do sistema antigo:

```text
Hostinger -> frontend/site
Linode    -> API/backend
Banco     -> provavelmente na Linode ou acessivel pela API
```

Evidencias:

- Front antigo chamava:

```text
http://li927-43.members.linode.com:9090
```

- Cliente possui recibo Linode/Akamai em 01/07/2026.

Hostinger tambem possui MySQL, mas isso nao prova que o sistema antigo usa esse banco.

Para confirmar:

- Abrir phpMyAdmin da Hostinger.
- Procurar tabelas como `clientes`, `funcionarios`, `agendamentos`, `laudos`, `usuarios`.
- Comparar com configuracao real da API antiga.
- Acessar painel/servidor Linode se possivel.

