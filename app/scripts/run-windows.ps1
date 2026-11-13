# Rúnheim Live Hall — Windows serve path (no Docker, ever).
# Starts the framework-dependent win-x64 publish. Requires the .NET 8
# runtime (or SDK) installed. Publish first if needed:
#   .\publish-all.ps1
# Then:  .\run-windows.ps1
$ErrorActionPreference = 'Stop'

$appDir  = Split-Path -Parent $PSScriptRoot          # app/
$repoDir = Split-Path -Parent $appDir               # repo root
$pubDir  = Join-Path $appDir 'publish\win-x64'
$exe     = Join-Path $pubDir 'Runheim.Server.exe'
$dll     = Join-Path $pubDir 'Runheim.Server.dll'

if (-not (Test-Path $exe) -and -not (Test-Path $dll)) {
    Write-Host "No win-x64 publish found in $pubDir"
    Write-Host "Run .\publish-all.ps1 first, then run this script again."
    exit 1
}

# Point the hall at the repo's own art and data unless the caller set them.
if (-not $env:Runheim__ArtRoot)  { $env:Runheim__ArtRoot  = Join-Path $repoDir 'art' }
if (-not $env:Runheim__DataRoot) { $env:Runheim__DataRoot = Join-Path $repoDir 'data' }
if (-not $env:ASPNETCORE_URLS)   { $env:ASPNETCORE_URLS   = 'http://0.0.0.0:4477' }

Write-Host "Rúnheim Live Hall starting at $env:ASPNETCORE_URLS (Ctrl+C to stop)"
if (Test-Path $exe) {
    & $exe
} else {
    & dotnet $dll
}
