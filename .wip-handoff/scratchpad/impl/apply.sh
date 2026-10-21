#!/bin/bash
# Applies the green work from the scratchpad into the slice-2 worktree (after the red commit is pushed).
set -eo pipefail
S=/tmp/claude-1000/-home-thor-repos-agentic-workflow/9d94207a-bc5b-4ae0-8853-ea701a7de3cc/scratchpad/impl
W=/home/thor/repos/aw-wt/sindri-ui-context-2
U=$W/src/Sindri/Aw.Sindri.Ui
cp $S/ContextMapLayout*.cs $U/Views/Context/Map/
cp $S/ContextMapViewModel.cs $S/MapNodeViewModel.cs $S/MapPillViewModel.cs $S/MapBandViewModel.cs $S/MapAnchorViewModel.cs $U/ViewModels/Context/
cp $S/Coverage.cs $U/ViewModels/Context/CoverageWords.cs
sed -i 's/internal static class Coverage$/internal static class CoverageWords/' $U/ViewModels/Context/CoverageWords.cs
sed -i 's/(CoverageAreas, CoverageLead, CoverageRest) = Coverage.Words(view);/(CoverageAreas, CoverageLead, CoverageRest) = CoverageWords.Words(view);/' $U/ViewModels/Context/ContextMapViewModel.cs
cp $S/page/ContextPageViewModel.Lens.cs $U/ViewModels/Context/ContextPageViewModel.Lens.cs
cp $S/page/moved $U/ViewModels/Context/ContextPageViewModel.View.cs
git -C $W rm -q -f src/Sindri/Aw.Sindri.Ui/ViewModels/Context/ContextBandViewModel.cs || true
cp $S/views/MapNodeView.axaml $S/views/MapNodeView.axaml.cs $S/views/ContextMapWorld.cs $S/views/ContextMapView.axaml $S/views/ContextMapView.axaml.cs $U/Views/Context/Map/
cp $S/views/ContextPage.axaml $S/views/ContextPage.axaml.cs $U/Views/Context/
cp $S/tests/ContextMapFramesTests.cs $W/tests/Aw.Sindri.Ui.Tests/Views/
cp $S/tests/ContextMapViewModelTests.cs $W/tests/Aw.Sindri.Ui.Tests/Context/
python3 $S/apply_tests.py
python3 - <<'PY'
p='/home/thor/repos/aw-wt/sindri-ui-context-2/src/Sindri/Aw.Sindri.Ui/Theme/Icons.axaml'
s=open(p).read()
add=open('/tmp/claude-1000/-home-thor-repos-agentic-workflow/9d94207a-bc5b-4ae0-8853-ea701a7de3cc/scratchpad/impl/Icons.kinds.xml').read()
s=s.replace('</ResourceDictionary>',add+'</ResourceDictionary>')
open(p,'w').write(s)
PY
echo applied
python3 - <<'PY'
import re
p='/home/thor/repos/aw-wt/sindri-ui-context-2/src/Sindri/Aw.Sindri.Ui/ViewModels/Labels.Context.cs'
s=open(p).read()
for name in ['ContextMore','ContextMapLine','ContextDraftMark','ContextIntent','ContextProduct','ContextCode','ContextEdges','ContextCoverage','ContextSearched','ContextModelSuggested','ContextSuggestedFile','ContextConflict','ContextSymbols','ContextIndexNotBuilt','ContextBandHeader','ContextCoverIntent','ContextCoverProduct','ContextCoverCode']:
    m=re.search(r'internal (?:const string|static string) '+name+r'\b',s)
    assert m,name
    start=s.rfind('\n\n',0,m.start())+2
    end=s.index(';\n',m.start())+2
    s=s[:start]+s[end:].lstrip('\n')
s=re.sub(r'\n{3,}','\n\n',s)
open(p,'w').write(s)
PY
echo labels-cleaned
