#!/usr/bin/env bash
set -euo pipefail
# From the repository folder, where kofrenot.sln is.
cd "$(dirname "$0")/.."

dotnet restore
dotnet build -c Release --no-restore
# -tl:off: the terminal logger would hide the coverage summary.
dotnet test -c Release --no-build -tl:off
