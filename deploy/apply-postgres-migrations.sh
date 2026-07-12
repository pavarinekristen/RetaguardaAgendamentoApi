#!/usr/bin/env bash
# Aplica as migrations Postgres (postgres-migrations/*.sql) no container agenda-db-postgres.
# Equivalente Linux do apply-postgres-migrations.ps1. Scripts sao idempotentes.
#
# Uso: ./apply-postgres-migrations.sh [dir-das-migrations]
#      (padrao: ../postgres-migrations relativo a este script)

set -euo pipefail

CONTAINER="agenda-db-postgres"
DATABASE="retaguarda_agendamento"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MIGRATIONS_DIR="${1:-$SCRIPT_DIR/../postgres-migrations}"

if ! docker ps --filter "name=$CONTAINER" --format '{{.Names}}' | grep -qx "$CONTAINER"; then
    echo "ERRO: container '$CONTAINER' nao esta rodando." >&2
    echo "Rode: docker compose -f docker-compose.postgres.prod.yml up -d" >&2
    exit 1
fi

for arq in "$MIGRATIONS_DIR"/*.sql; do
    nome="$(basename "$arq")"
    echo "==> Aplicando $nome"
    docker cp "$arq" "$CONTAINER:/tmp/$nome"
    docker exec "$CONTAINER" psql -U postgres -d "$DATABASE" -v ON_ERROR_STOP=1 -f "/tmp/$nome"
done

echo "Migrations aplicadas com sucesso."
echo "ATENCAO: 002_roles.sql cria agenda_user/agenda_admin com senha padrao de desenvolvimento."
echo "Em producao, troque as senhas AGORA e use as novas nas connection strings:"
echo "  docker exec -it $CONTAINER psql -U postgres -d $DATABASE -c \"ALTER ROLE agenda_user  PASSWORD 'NOVA_SENHA_FORTE_1';\""
echo "  docker exec -it $CONTAINER psql -U postgres -d $DATABASE -c \"ALTER ROLE agenda_admin PASSWORD 'NOVA_SENHA_FORTE_2';\""
