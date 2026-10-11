using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Testing;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class ScriptedUiEvidenceContractTests : UiEvidenceContract
{
    private readonly InMemoryRepos _repos = new();

    protected override (IUiEvidence Ui, UiRunRequest Request, string ScriptSha) Arrange()
    {
        _repos.Write("/repo/s.json", "{}");
        return (new ScriptedUiEvidence(_repos), new UiRunRequest("/repo/s.json", "/state/runs/1-ui", "abc", "/repo", "/state/runs/1-ui.log"), _repos.Sha256("/repo/s.json").Value);
    }

    protected override IUiEvidence Failing() => new ScriptedUiEvidence(_repos).Fails(BugFixErrors.UiEvidenceUnavailable.With("no summary"));
}
