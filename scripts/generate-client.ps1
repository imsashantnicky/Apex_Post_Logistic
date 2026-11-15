# Regenerates src/ApexPost.Client/Generated/ApexPostApiClient.g.cs from contracts/v2.json with NSwag.
# The NSwag CLI (nswag.consolecore, pinned below) runs through `dotnet tool exec` (.NET 10 SDK) without being
# installed; all generator settings are in src/ApexPost.Client/nswag.json. Run scripts/export-contracts.ps1 first
# when the API contract changed.
# Usage: scripts/generate-client.ps1
$ErrorActionPreference = 'Stop'

$nswag = 'nswag.consolecore@14.7.1'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    # The exact command: NSwag reads contracts/v2.json and writes Generated/ApexPostApiClient.g.cs (paths relative
    # to nswag.json).
    dotnet tool exec $nswag --yes -- run src/ApexPost.Client/nswag.json
    if ($LASTEXITCODE -ne 0) { throw 'NSwag generation failed.' }

    dotnet build src/ApexPost.Client/ApexPost.Client.csproj -c Release --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'The generated client does not build.' }

    Write-Host 'Wrote src/ApexPost.Client/Generated/ApexPostApiClient.g.cs'
}
finally {
    Pop-Location
}
