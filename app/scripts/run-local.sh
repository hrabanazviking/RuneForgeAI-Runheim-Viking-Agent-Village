#!/bin/sh
# Developer's run: build and serve the hall straight from the project.
# In a fresh clone the server finds the repo's art/ and data/ by walking up
# from the project folder (see Program.cs), so no configuration is needed.
set -eu

APP_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
DOTNET=${DOTNET:-dotnet}
command -v "$DOTNET" >/dev/null 2>&1 || DOTNET="$HOME/dotnet/dotnet"
export ASPNETCORE_URLS=${ASPNETCORE_URLS:-http://0.0.0.0:4477}
exec "$DOTNET" run --project "$APP_DIR/Runheim.Server/Runheim.Server.csproj"
