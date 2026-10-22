using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Testing.BugFix;

namespace Aw.Sindri.Tests.BugFix;

/// <summary>The in-memory bugfix-state honours the port's contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryBugFixStateContractTests : BugFixStateContract
{
    private const string Started = "/state/eng-1";

    private readonly InMemoryBugFixState _fake = new InMemoryBugFixState().Init(Started);

    private int _commits;

    protected override string StateDirectory => Started;

    protected override string EmptyDirectory => "/state/none";

    protected override FrozenCheck Check { get; } = new("test", "tests/bug.test.sh", ["bash", "tests/bug.test.sh"], ".");

    protected override IBugFixState Create() => _fake;

    protected override CandidateSource Commit(bool fixes)
    {
        var branch = (fixes ? "fix-" : "miss-") + (++_commits).ToString(CultureInfo.InvariantCulture);
        _ = fixes ? _fake.Fixes(branch) : _fake;
        return new CandidateSource(branch, "/work/" + branch);
    }
}
