#!/usr/bin/env bash
# Ensures the Pexilio role and databases exist on the local PostgreSQL server.
# Creates them only when missing. Does not drop, rename, or rewrite any database.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ENV_FILE="${PEXILIO_ENV_FILE:-${ROOT}/config/dev/local.env}"
PGHOST="${PEXILIO_PGHOST:-127.0.0.1}"
PGPORT="${PEXILIO_PGPORT:-5432}"

if ! pg_isready -h "${PGHOST}" -p "${PGPORT}" >/dev/null 2>&1; then
  echo "PostgreSQL is not accepting connections on ${PGHOST}:${PGPORT}." >&2
  echo "Start the system service with:" >&2
  echo "  sudo systemctl enable --now postgresql" >&2
  exit 1
fi

mkdir -p "$(dirname "${ENV_FILE}")"

if [[ -f "${ENV_FILE}" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "${ENV_FILE}"
  set +a
  password="${PEXILIO_CONNECTION##*Password=}"
  password="${password//\'/}"
else
  password="$(openssl rand -hex 24)"
fi

if [[ -z "${password}" || "${password}" == "replace-me" ]]; then
  echo "Could not read a development password from ${ENV_FILE}." >&2
  exit 1
fi

signing_key="${PEXILIO_JWT_SIGNING_KEY:-}"
signing_key="${signing_key//\'/}"
if [[ -z "${signing_key}" || "${signing_key}" == "replace-with-generated-key" ]]; then
  signing_key="$(openssl rand -hex 32)"
fi

umask 077
cat > "${ENV_FILE}" <<EOF
PEXILIO_CONNECTION='Host=${PGHOST};Port=${PGPORT};Database=pexilio_dev;Username=pexilio_dev;Password=${password}'
PEXILIO_TEST_CONNECTION='Host=${PGHOST};Port=${PGPORT};Database=pexilio_test;Username=pexilio_dev;Password=${password}'
PEXILIO_API_URL='http://127.0.0.1:5080'
PEXILIO_JWT_SIGNING_KEY='${signing_key}'
EOF
chmod 600 "${ENV_FILE}"

psql -h "${PGHOST}" -p "${PGPORT}" -U postgres -d postgres -v ON_ERROR_STOP=1 -v pw="${password}" <<'SQL'
SELECT format('CREATE ROLE pexilio_dev LOGIN PASSWORD %L', :'pw')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'pexilio_dev')\gexec
SELECT 'CREATE DATABASE pexilio_dev OWNER pexilio_dev'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'pexilio_dev')\gexec
SELECT 'CREATE DATABASE pexilio_test OWNER pexilio_dev'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'pexilio_test')\gexec
SQL

export PGPASSWORD="${password}"
psql -h "${PGHOST}" -p "${PGPORT}" -U pexilio_dev -d pexilio_dev -v ON_ERROR_STOP=1 -c 'SELECT current_user, current_database();' >/dev/null
psql -h "${PGHOST}" -p "${PGPORT}" -U pexilio_dev -d pexilio_test -v ON_ERROR_STOP=1 -c 'SELECT current_user, current_database();' >/dev/null
unset PGPASSWORD

echo "Pexilio databases are ready on ${PGHOST}:${PGPORT}."
echo "Connection settings: ${ENV_FILE}"
echo "Other databases on this server were not modified."
