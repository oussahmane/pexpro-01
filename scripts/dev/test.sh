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
dotnet test pexilio.slnx
npm test --prefix apps/desktop
