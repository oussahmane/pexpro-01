#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck disable=SC1091
source "${ROOT}/scripts/dev/dotnet-env.sh"
set -a
# shellcheck disable=SC1091
source "${ROOT}/config/dev/local.env"
set +a

cd "${ROOT}"

database="${PEXILIO_CONNECTION#*Database=}"
database="${database%%;*}"
database="${database//\'/}"
if [[ "${database}" != "pexilio_dev" ]]; then
  echo "Refusing to migrate database '${database}'. This command only updates pexilio_dev." >&2
  exit 1
fi
echo "Migration target: ${database} on the connection string host and port."

dotnet tool restore
dotnet tool run dotnet-ef -- database update \
  --project src/Pexilio.Infrastructure \
  --startup-project src/Pexilio.Api
