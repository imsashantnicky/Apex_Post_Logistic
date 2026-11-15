<#
.SYNOPSIS
Exports the live OpenAPI documents to contracts/v1.json and contracts/v2.json.

.DESCRIPTION
Starts ApexPost.Api in Development (the documents exist only there), downloads each document, pretty-prints it with
scripts/format-json.cs and stops the API. Needs the user-secret ConnectionStrings:ApexPostDb (setup-secrets.bat).

.EXAMPLE
./scripts/export-contracts.ps1 -Port 5199
#>
param([int] $Port = 5199)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$base = "http://127.0.0.1:$Port"
$work = Join-Path ([IO.Path]::GetTempPath()) ("apexpost-contracts-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$api = $null

Push-Location $root
try {
    dotnet build src/ApexPost.Api/ApexPost.Api.csproj -c Release --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'The API did not build.' }

    Write-Host "Starting ApexPost.Api on $base (Development)..."
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = $base
    $api = Start-Process dotnet -PassThru -NoNewWindow `
        -RedirectStandardOutput (Join-Path $work 'api.log') -RedirectStandardError (Join-Path $work 'api.err') `
        -ArgumentList 'src/ApexPost.Api/bin/Release/net10.0/ApexPost.Api.dll', '--contentRoot', "`"$root/src/ApexPost.Api`""

    $ready = $false
    for ($attempt = 0; $attempt -lt 90 -and -not $ready; $attempt++) {
        if ($api.HasExited) { throw "The API stopped before the documents were served. See $work." }
        try {
            $ready = (Invoke-WebRequest "$base/openapi/v1.json" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200
        }
        catch {
            Start-Sleep -Seconds 1
        }
    }
    if (-not $ready) { throw 'The API did not serve /openapi/v1.json within 90 seconds.' }

    New-Item -ItemType Directory -Force -Path contracts | Out-Null
    foreach ($version in 'v1', 'v2') {
        $download = Join-Path $work "$version.json"
        Invoke-WebRequest "$base/openapi/$version.json" -UseBasicParsing -OutFile $download
        dotnet run scripts/format-json.cs -- $download "contracts/$version.json"
        if ($LASTEXITCODE -ne 0) { throw "Formatting $version failed." }
        Write-Host "Wrote contracts/$version.json"
    }
}
finally {
    if ($null -ne $api -and -not $api.HasExited) {
        Stop-Process -Id $api.Id -Force
    }
    Remove-Item Env:ASPNETCORE_ENVIRONMENT, Env:ASPNETCORE_URLS -ErrorAction SilentlyContinue
    Pop-Location
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
