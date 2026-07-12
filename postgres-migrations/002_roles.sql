-- Roles/usuarios da API no Postgres (equivalente ao 002 do MySQL).
--   agenda_user  -> conexao padrao (schema retaguarda_agendamento)
--   agenda_admin -> conexao administrativa (schema agenda_operacional + empresa)
-- Idempotente: pode rodar novamente sem erro.

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'agenda_user') THEN
    CREATE ROLE agenda_user LOGIN PASSWORD 'AgendaUser@2026';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'agenda_admin') THEN
    CREATE ROLE agenda_admin LOGIN PASSWORD 'AgendaAdmin@2026';
  END IF;
END
$$;

GRANT CONNECT ON DATABASE retaguarda_agendamento TO agenda_user, agenda_admin;

-- ---- schema retaguarda_agendamento: agenda_user tem CRUD; agenda_admin le/atualiza empresa
GRANT USAGE ON SCHEMA retaguarda_agendamento TO agenda_user, agenda_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES    IN SCHEMA retaguarda_agendamento TO agenda_user;
GRANT USAGE, SELECT, UPDATE          ON ALL SEQUENCES IN SCHEMA retaguarda_agendamento TO agenda_user;
GRANT SELECT, UPDATE ON retaguarda_agendamento.empresa TO agenda_admin;
ALTER DEFAULT PRIVILEGES IN SCHEMA retaguarda_agendamento
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO agenda_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA retaguarda_agendamento
    GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO agenda_user;

-- ---- schema agenda_operacional: agenda_admin cria/ajusta tabelas em runtime
GRANT USAGE, CREATE ON SCHEMA agenda_operacional TO agenda_admin;
GRANT ALL ON ALL TABLES    IN SCHEMA agenda_operacional TO agenda_admin;
GRANT ALL ON ALL SEQUENCES IN SCHEMA agenda_operacional TO agenda_admin;
ALTER DEFAULT PRIVILEGES IN SCHEMA agenda_operacional
    GRANT ALL ON TABLES TO agenda_admin;
ALTER DEFAULT PRIVILEGES IN SCHEMA agenda_operacional
    GRANT ALL ON SEQUENCES TO agenda_admin;
