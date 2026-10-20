#!/bin/bash
cd /home/thor/repos/aw-wt/sindri-ui-f3-b4-agents-shell || exit 2
echo "== build"; dotnet build tests/Aw.Sindri.Ui.Tests -warnaserror 2>&1 | tail -25
echo "== unit"; dotnet test --project tests/Aw.Sindri.Ui.Tests --filter-trait "Lane=unit" --ignore-exit-code 8 2>&1 | tail -60
echo "== integration"; dotnet test --project tests/Aw.Sindri.Ui.Tests --filter-class "*SessionSheetTests" --filter-class "*AgentsPageTests" --filter-class "*ContextPageWindowTests" --filter-class "*CommandPaletteTests" --filter-class "*GoldenContractTests" --ignore-exit-code 8 2>&1 | tail -60
echo "== rendering"; dotnet test --project tests/Aw.Sindri.Ui.Rendering.Tests --ignore-exit-code 8 2>&1 | tail -30
echo "== arch"; dotnet test --project tests/Aw.Architecture.Tests --ignore-exit-code 8 2>&1 | tail -30
echo "== format"; dotnet format --verify-no-changes --no-restore --include $(git diff --name-only | grep '\.cs$' | tr '\n' ' ') tests/Aw.Sindri.Ui.Tests/Shell/PlainWordsTests.cs 2>&1 | tail -30
