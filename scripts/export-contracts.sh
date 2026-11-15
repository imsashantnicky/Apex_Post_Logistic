#!/usr/bin/env bash
# Exports the live OpenAPI documents to contracts/v1.json and contracts/v2.json.
# Starts ApexPost.Api in Development (the documents exist only there), downloads each document, pretty-prints it
# with scripts/format-json.cs and stops the API. Needs the user-secret ConnectionStrings:ApexPostDb (setup-secrets).
# Usage: scripts/export-contracts.sh [port]
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
port="${1:-5199}"
base="http://127.0.0.1:${port}"
work="$(mktemp -d)"
api_pid=""

cleanup() {
  if [[ -n "${api_pid}" ]]; then
    kill "${api_pid}" 2>/dev/null || true
    wait "${api_pid}" 2>/dev/null || true
  fi
  rm -rf "${work}"
}
trap cleanup EXIT

cd "${root}"
dotnet build src/ApexPost.Api/ApexPost.Api.csproj -c Release --nologo -v quiet

echo "Starting ApexPost.Api on ${base} (Development)..."
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="${base}" \
  dotnet src/ApexPost.Api/bin/Release/net10.0/ApexPost.Api.dll --contentRoot "${root}/src/ApexPost.Api" \
  > "${work}/api.log" 2>&1 &
api_pid=$!

for _ in $(seq 1 90); do
  if curl -fsS -o /dev/null "${base}/openapi/v1.json" 2>/dev/null; then
    break
  fi
  if ! kill -0 "${api_pid}" 2>/dev/null; then
    cat "${work}/api.log" >&2
    echo "The API stopped before the documents were served." >&2
    exit 1
  fi
  sleep 1
done

mkdir -p contracts
for version in v1 v2; do
  curl -fsS "${base}/openapi/${version}.json" -o "${work}/${version}.json"
  dotnet run scripts/format-json.cs -- "${work}/${version}.json" "contracts/${version}.json"
  echo "Wrote contracts/${version}.json"
done
