set -eo pipefail
cd /home/thor/repos/aw-wt/sindri-ui-workflows
H="timeout 3600 $HOME/.agentic-workflow/bin-r1b/heavy dotnet"
$H format src/Sindri/Aw.Sindri.Ui/Aw.Sindri.Ui.csproj --verify-no-changes --no-restore --include src/Sindri/Aw.Sindri.Ui/ViewModels/Workflows/RunStatus.cs src/Sindri/Aw.Sindri.Ui/ViewModels/Workflows/RunStatusKind.cs src/Sindri/Aw.Sindri.Ui/ViewModels/Workflows/InstanceRowViewModel.cs src/Sindri/Aw.Sindri.Ui/ViewModels/Labels.Board.cs
$H format tests/Aw.Sindri.Ui.Tests/Aw.Sindri.Ui.Tests.csproj --verify-no-changes --no-restore --include tests/Aw.Sindri.Ui.Tests/Workflows/RunStatusTests.cs tests/Aw.Sindri.Ui.Tests/Workflows/InstanceRowViewModelTests.cs tests/Aw.Sindri.Ui.Tests/Theme/ContrastTests.cs tests/Aw.Sindri.Ui.Tests/Theme/TokenTests.cs tests/Aw.Sindri.Ui.Tests/Theme/ThemeResourcesTests.cs
echo FORMAT-OK
$H build src/Sindri/Aw.Sindri.Ui/Aw.Sindri.Ui.csproj -warnaserror 2>&1 | tail -3
echo UNIT
$H test --project tests/Aw.Sindri.Ui.Tests --filter-trait "Lane=unit" 2>&1 | tail -8
echo INTEGRATION
$H test --project tests/Aw.Sindri.Ui.Tests --filter-trait "Lane=integration" 2>&1 | tail -8
echo RENDERING
$H test --project tests/Aw.Sindri.Ui.Rendering.Tests 2>&1 | tail -8
echo ARCH
$H test --project tests/Aw.Architecture.Tests 2>&1 | tail -8
echo GATE-DONE
