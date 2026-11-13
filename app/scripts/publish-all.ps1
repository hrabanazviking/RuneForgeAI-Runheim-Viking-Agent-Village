# Publish Rúnheim for every supported RID (framework-dependent).
# Proof level per RID: "publishes clean" — a publish is not a run.
# Run the result on its own OS: scripts/run-windows.ps1 (Windows),
# app/runheim.service (Linux), app/runheim.plist (macOS).
$ErrorActionPreference = 'Stop'

$appDir = Split-Path -Parent $PSScriptRoot
$proj   = Join-Path $appDir 'Runheim.Server\Runheim.Server.csproj'
$rids   = 'win-x64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64'

foreach ($rid in $rids) {
    $out = Join-Path $appDir "publish\$rid"
    Write-Host "=== $rid -> $out"
    dotnet publish $proj -c Release -r $rid --self-contained false -o $out
    if ($LASTEXITCODE -ne 0) { throw "publish failed for $rid" }
}
Write-Host "All RIDs published."
