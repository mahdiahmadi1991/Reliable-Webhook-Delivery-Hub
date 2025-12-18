#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="${SCRIPT_DIR%/scripts}"

cleanup() {
  docker compose -f "${REPO_ROOT}/docker-compose.yml" -f "${REPO_ROOT}/docker-compose.test.yml" down -v >/dev/null 2>&1 || true
}

trap cleanup EXIT

docker compose -f "${REPO_ROOT}/docker-compose.yml" -f "${REPO_ROOT}/docker-compose.test.yml" up --build --abort-on-container-exit --exit-code-from tests
