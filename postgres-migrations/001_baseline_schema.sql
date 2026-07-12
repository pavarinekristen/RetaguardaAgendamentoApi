-- Baseline schema (PostgreSQL) para RetaguardaAgendamentoAPI.
-- Os dois "bancos" do MySQL viram dois SCHEMAS no mesmo banco Postgres:
--   retaguarda_agendamento  -> cadastro / autenticacao
--   agenda_operacional      -> sincronizacao / integracao (tabelas tambem criadas em runtime)
-- Convencao: identificadores em minusculo e sem aspas. O SQL em maiusculas do codigo
-- e "case-folded" para minusculo pelo Postgres, entao casa com estas tabelas.

CREATE SCHEMA IF NOT EXISTS retaguarda_agendamento;
CREATE SCHEMA IF NOT EXISTS agenda_operacional;

-- =========================================================
-- Schema retaguarda_agendamento
-- =========================================================
CREATE TABLE IF NOT EXISTS retaguarda_agendamento.schema_migration (
  id              SERIAL PRIMARY KEY,
  version         VARCHAR(30)  NOT NULL,
  file_name       VARCHAR(255) NOT NULL,
  checksum_sha256 VARCHAR(64)  NOT NULL,
  applied_at      TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT uk_schema_migration_version UNIQUE (version),
  CONSTRAINT uk_schema_migration_file    UNIQUE (file_name)
);

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
    ON retaguarda_agendamento.ret_email_token (token_hash);

-- =========================================================
-- Schema agenda_operacional
-- =========================================================
CREATE TABLE IF NOT EXISTS agenda_operacional.agenda_sync_execucao (
  id              BIGSERIAL PRIMARY KEY,
  id_empresa      INTEGER NOT NULL,
  cnpj            VARCHAR(14) NOT NULL,
  dispositivo_id  VARCHAR(64) NOT NULL,
  iniciado_em     TIMESTAMP NOT NULL,
  finalizado_em   TIMESTAMP,
  total_tabelas   INTEGER NOT NULL DEFAULT 0,
  total_registros INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_agenda_sync_execucao_empresa
    ON agenda_operacional.agenda_sync_execucao (id_empresa);

CREATE TABLE IF NOT EXISTS agenda_operacional.agenda_sync_registro (
  id              BIGSERIAL PRIMARY KEY,
  id_execucao     BIGINT NOT NULL,
  id_empresa      INTEGER NOT NULL,
  tabela          VARCHAR(128) NOT NULL,
  id_local        VARCHAR(128) NOT NULL,
  dispositivo_id  VARCHAR(64) NOT NULL,
  dados_json      TEXT NOT NULL,
  hash_sha256     VARCHAR(64) NOT NULL,
  sincronizado_em TIMESTAMP NOT NULL,
  CONSTRAINT uk_agenda_sync_registro_tenant_tabela_id_device
      UNIQUE (id_empresa, tabela, id_local, dispositivo_id)
);
CREATE INDEX IF NOT EXISTS ix_agenda_sync_registro_execucao
    ON agenda_operacional.agenda_sync_registro (id_execucao);
CREATE INDEX IF NOT EXISTS ix_agenda_sync_registro_empresa
    ON agenda_operacional.agenda_sync_registro (id_empresa);

CREATE TABLE IF NOT EXISTS agenda_operacional.agenda_sync_conflito (
  id             BIGSERIAL PRIMARY KEY,
  id_empresa     INTEGER NOT NULL,
  tabela         VARCHAR(128) NOT NULL,
  id_local       VARCHAR(128) NOT NULL,
  dispositivo_id VARCHAR(64) NOT NULL,
  dados_local    TEXT,
  dados_remoto   TEXT,
  motivo         VARCHAR(500),
  status         VARCHAR(30) NOT NULL DEFAULT 'PENDENTE',
  criado_em      TIMESTAMP NOT NULL,
  resolvido_em   TIMESTAMP
);
CREATE INDEX IF NOT EXISTS ix_agenda_sync_conflito_empresa
    ON agenda_operacional.agenda_sync_conflito (id_empresa);

CREATE TABLE IF NOT EXISTS agenda_operacional.agenda_dispositivo (
  id                      BIGSERIAL PRIMARY KEY,
  id_empresa              INTEGER NOT NULL,
  dispositivo_id          VARCHAR(64) NOT NULL,
  nome                    VARCHAR(120),
  ultima_sincronizacao_em TIMESTAMP,
  ativo                   CHAR(1) NOT NULL DEFAULT 'S',
  CONSTRAINT uk_agenda_dispositivo_empresa_dispositivo
      UNIQUE (id_empresa, dispositivo_id)
);

CREATE TABLE IF NOT EXISTS agenda_operacional.agenda_auditoria_operacional (
  id             BIGSERIAL PRIMARY KEY,
  id_empresa     INTEGER NOT NULL,
  tabela         VARCHAR(128) NOT NULL,
  id_local       VARCHAR(128) NOT NULL,
  dispositivo_id VARCHAR(64) NOT NULL,
  acao           VARCHAR(30) NOT NULL,
  dados_json     TEXT,
  criado_em      TIMESTAMP NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_agenda_auditoria_empresa_tabela
    ON agenda_operacional.agenda_auditoria_operacional (id_empresa, tabela);

CREATE TABLE IF NOT EXISTS agenda_operacional.integracao_outbox (
  id           BIGSERIAL PRIMARY KEY,
  id_empresa   INTEGER NOT NULL,
  origem       VARCHAR(60) NOT NULL,
  entidade     VARCHAR(128) NOT NULL,
  id_local     VARCHAR(128) NOT NULL,
  payload_json TEXT NOT NULL,
  status       VARCHAR(30) NOT NULL DEFAULT 'PROCESSADO',
  criado_em    TIMESTAMP NOT NULL,
  processado_em TIMESTAMP
);
CREATE INDEX IF NOT EXISTS ix_integracao_outbox_empresa_status
    ON agenda_operacional.integracao_outbox (id_empresa, status);

CREATE TABLE IF NOT EXISTS agenda_operacional.integracao_conflito (
  id           BIGSERIAL PRIMARY KEY,
  id_empresa   INTEGER NOT NULL,
  entidade     VARCHAR(128) NOT NULL,
  id_local     VARCHAR(128) NOT NULL,
  motivo       VARCHAR(500),
  payload_json TEXT,
  status       VARCHAR(30) NOT NULL DEFAULT 'PENDENTE',
  criado_em    TIMESTAMP NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_integracao_conflito_empresa
    ON agenda_operacional.integracao_conflito (id_empresa);

CREATE TABLE IF NOT EXISTS agenda_operacional.integracao_log (
  id         BIGSERIAL PRIMARY KEY,
  id_empresa INTEGER NOT NULL,
  entidade   VARCHAR(128) NOT NULL,
  id_local   VARCHAR(128),
  evento     VARCHAR(60) NOT NULL,
  mensagem   VARCHAR(1000),
  criado_em  TIMESTAMP NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_integracao_log_empresa
    ON agenda_operacional.integracao_log (id_empresa);
