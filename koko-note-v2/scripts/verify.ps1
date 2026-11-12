$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    dotnet restore KOKONote.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'restore failed' }
    dotnet build KOKONote.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'build failed' }
    dotnet test KOKONote.sln -c Release --no-build --logger 'trx;LogFileName=windows.trx' --results-directory TestResults
    if ($LASTEXITCODE -ne 0) { throw 'test failed' }
    Write-Host 'restore: pass | build: pass | test: pass | format: skipped'
} finally {
    Pop-Location
}
