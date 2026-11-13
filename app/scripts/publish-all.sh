#!/bin/sh
# Publish Rúnheim for every supported RID (framework-dependent).
# Proof level per RID: "publishes clean" — a publish is not a run.
# Run the result on its own OS: app/scripts/run-windows.ps1 (Windows),
# app/runheim.service (Linux), app/runheim.plist (macOS).
set -eu

APP_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
PROJ="$APP_DIR/Runheim.Server/Runheim.Server.csproj"
DOTNET=${DOTNET:-dotnet}
command -v "$DOTNET" >/dev/null 2>&1 || DOTNET="$HOME/dotnet/dotnet"

for rid in win-x64 linux-x64 linux-arm64 osx-x64 osx-arm64; do
  out="$APP_DIR/publish/$rid"
  echo "=== $rid -> $out"
  "$DOTNET" publish "$PROJ" -c Release -r "$rid" --self-contained false -o "$out"
done
echo "All RIDs published."
