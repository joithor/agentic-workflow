#!/usr/bin/env bash
# .devcontainer/post-create.sh — runs once after the container is created. Prints the tool versions and
# restores the C# solution in locked mode. It installs nothing else (the TS packages stay `npm ci` per
# package, on demand). It writes only to the workspace and the container user's NuGet and dotnet caches.
set -euo pipefail
dotnet --version
node --version
uv --version
dotnet restore AgenticWorkflow.slnx --locked-mode
