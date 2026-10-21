#!/usr/bin/env bash
set -eo pipefail
cd /home/thor/repos/aw-wt/sindri-ui-needs-you-2
dotnet build tests/Aw.Sindri.Ui.Tests -warnaserror
dotnet test --project tests/Aw.Sindri.Ui.Tests --no-build --filter-trait "Lane=unit" --ignore-exit-code 8
