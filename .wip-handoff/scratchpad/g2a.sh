set -eo pipefail
cd /home/thor/repos/aw-wt/sindri-ui-workflows-2
timeout 3600 $HOME/.agentic-workflow/bin-r1b/heavy dotnet test --project tests/Aw.Sindri.Ui.Tests --filter-trait "Lane=unit" 2>&1 | tail -60
