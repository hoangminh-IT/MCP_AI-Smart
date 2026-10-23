#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
# The context and migrations live in Infrastructure; API supplies host configuration.
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
dotnet ef "$@" \
  --project backend/SmartInventory.Infrastructure \
  --startup-project backend/SmartInventory.Api \
  --context AppDbContext
