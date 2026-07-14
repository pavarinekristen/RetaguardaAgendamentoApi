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

1. WPF grava dados no SQLite (todo save carimba `AtualizadoEm`).
2. WPF monta snapshot **incremental** das tabelas locais: somente registros com
   `SincronizadoEm IS NULL` ou `AtualizadoEm > SincronizadoEm`.
3. WPF envia para:

```text
POST /sincroniza/agenda/snapshot
```

4. O snapshot e dividido em lotes de ate 1000 registros por requisicao
   (`MaxRegistrosPorLote`), mantendo cada payload em ~1-2 MB. Carga inicial
   grande (ex.: 48 mil registros importados) sobe em ~48 requisicoes sequenciais.
5. API valida token.
6. API identifica empresa.
7. API grava no MySQL operacional (upsert idempotente por `ID_LOCAL`).
8. Portal web consulta o MySQL operacional.
9. Apos cada lote aceito, o WPF marca como sincronizados os IDs daquele lote com
   `AtualizadoEm <= inicio do snapshot`. Se a conexao cair no meio, o que ja
   subiu nao e reenviado na proxima tentativa; o que faltou continua pendente.

Snapshot completo:

- `SincronizarAsync(completo: true)` envia todas as linhas e marca o payload com
  `snapshotCompleto = true`. Uso: recuperacao/recarga do servidor.
- No servidor, `Sincronizacao:MarcarAusentesComoExcluidos` so atua quando
  `snapshotCompleto = true` — em snapshot incremental, ausencia significa
  "sem alteracao", nunca exclusao.

SQLite local roda com `journal_mode=WAL` (aplicado no `MigrateAsync`): leitura da
tela nao bloqueia a escrita do sync automatico.

## Estado atual do snapshot

Pontos fortes:

- Funciona genericamente.
- Envio incremental: payload proporcional ao que mudou, nao ao tamanho do banco.
- Cria/atualiza tabelas finais conforme payload.
- Mantem `ID_EMPRESA`.
- Mantem `ID_LOCAL`.
- Marca ausentes como excluidos (somente em snapshot completo).
- Registra auditoria/outbox.

Pontos fracos:

- Ainda aceita tabelas/colunas dinamicas.
- Precisa de whitelist.
- Precisa de limites por payload.
- Sem retencao definida para historico de snapshot/auditoria no servidor.

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

