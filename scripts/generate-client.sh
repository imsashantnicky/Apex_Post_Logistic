#!/usr/bin/env bash
# Regenerates src/ApexPost.Client/Generated/ApexPostApiClient.g.cs from contracts/v2.json with NSwag.
# The NSwag CLI (nswag.consolecore, pinned below) runs through `dotnet tool exec` (.NET 10 SDK) without being
# installed; all generator settings are in src/ApexPost.Client/nswag.json. Run scripts/export-contracts.sh first
# when the API contract changed.
# Usage: scripts/generate-client.sh
set -euo pipefail

nswag="nswag.consolecore@14.7.1"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${root}"

# The exact command: NSwag reads contracts/v2.json and writes Generated/ApexPostApiClient.g.cs (paths relative to
# nswag.json).
dotnet tool exec "${nswag}" --yes -- run src/ApexPost.Client/nswag.json

dotnet build src/ApexPost.Client/ApexPost.Client.csproj -c Release --nologo -v quiet
echo "Wrote src/ApexPost.Client/Generated/ApexPostApiClient.g.cs"
