-- Usuario dedicado da API para o modulo de sincronizacao (substitui o uso de root na AdminConnection).
-- Privilegios minimos:
--   - ALL em agenda_operacional.*: o snapshot cria/ajusta tabelas dinamicamente nesse banco.
--   - SELECT/UPDATE em retaguarda_agendamento.EMPRESA: o snapshot atualiza dados cadastrais da empresa.
-- Se AGENDA_OPERACIONAL_DATABASE for sobrescrito para outro nome, conceder grants equivalentes nesse banco.

CREATE USER IF NOT EXISTS 'agenda_admin'@'%' IDENTIFIED BY 'AgendaAdmin@2026';

GRANT ALL PRIVILEGES ON `agenda_operacional`.* TO 'agenda_admin'@'%';
GRANT SELECT, UPDATE ON `retaguarda_agendamento`.`EMPRESA` TO 'agenda_admin'@'%';

FLUSH PRIVILEGES;
