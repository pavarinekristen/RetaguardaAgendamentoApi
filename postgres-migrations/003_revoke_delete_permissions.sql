-- Regra operacional: a API nao deve apagar dados.
-- Revoga permissao de DELETE do usuario padrao e tambem dos grants futuros.
-- Tambem habilita pg_trgm para acelerar pesquisas textuais com LIKE sem alterar dados.

CREATE EXTENSION IF NOT EXISTS pg_trgm;

REVOKE DELETE ON ALL TABLES IN SCHEMA retaguarda_agendamento FROM agenda_user;

ALTER DEFAULT PRIVILEGES IN SCHEMA retaguarda_agendamento
    REVOKE DELETE ON TABLES FROM agenda_user;
