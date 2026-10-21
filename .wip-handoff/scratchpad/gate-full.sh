#!/usr/bin/env bash
# usage: gate-full.sh <worktree> <touched .cs files relative to the worktree, space separated, for the scoped format>
set -eo pipefail
WT="$1"; shift
cd "$WT"
dotnet build tests/Aw.Sindri.Ui.Tests -warnaserror
dotnet build tests/Aw.Sindri.Ui.Rendering.Tests -warnaserror
dotnet build tests/Aw.Architecture.Tests -warnaserror
echo "=== unit lane"
dotnet test --project tests/Aw.Sindri.Ui.Tests --no-build --filter-trait "Lane=unit" --ignore-exit-code 8
echo "=== integration lane (full Ui.Tests)"
dotnet test --project tests/Aw.Sindri.Ui.Tests --no-build --filter-trait "Lane=integration" --ignore-exit-code 8
echo "=== rendering"
dotnet test --project tests/Aw.Sindri.Ui.Rendering.Tests --no-build --ignore-exit-code 8
echo "=== architecture"
dotnet test --project tests/Aw.Architecture.Tests --no-build --ignore-exit-code 8
echo "=== scoped format"
dotnet format src/Sindri/Aw.Sindri.Ui/Aw.Sindri.Ui.csproj --verify-no-changes --no-restore --include "$@"
echo "=== IDE0001"
dotnet format src/Sindri/Aw.Sindri.Ui/Aw.Sindri.Ui.csproj --verify-no-changes --no-restore --diagnostics IDE0001
echo "=== gate done"
