using Aw.Readiness.Core;
using Aw.Readiness.Testing;

namespace Aw.Readiness.Tests.History;

public sealed class RunDeltaTests
{
    private static readonly RepoName _repo = RepoName.From("agentic-workflow");
    private static readonly DateTimeOffset _earlier = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Lane", "unit")]
    public void No_previous_run_is_a_first_run_with_no_changes()
    {
        var current = RunRecord.From(_repo, _now, 2, [A.Finding("SV-1", CriterionState.Pass)]);

        var delta = RunDelta.Between(previous: null, current);

        Assert.True(delta.IsFirstRun);
        Assert.Null(delta.LevelBefore);
        Assert.Equal(2, delta.LevelAfter);
        Assert.Empty(delta.Changes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Changed_added_and_removed_criteria_are_listed_in_id_order()
    {
        var previous = RunRecord.From(_repo, _earlier, 2, [
            A.Finding("TS-1", CriterionState.Fail), A.Finding("SV-1", CriterionState.Pass), A.Finding("PE-9", CriterionState.Fail),
        ]);
        var current = RunRecord.From(_repo, _now, 3, [
            A.Finding("TS-1", CriterionState.Pass), A.Finding("SV-1", CriterionState.Pass), A.Finding("DE-1", CriterionState.Unknown),
        ]);

        var delta = RunDelta.Between(previous, current);

        Assert.False(delta.IsFirstRun);
        Assert.Equal(_earlier, delta.Since);
        Assert.Equal(2, delta.LevelBefore);
        Assert.Equal(
            [
                new StateChange(CriterionId.From("DE-1"), Before: null, CriterionState.Unknown),
                new StateChange(CriterionId.From("PE-9"), CriterionState.Fail, After: null),
                new StateChange(CriterionId.From("TS-1"), CriterionState.Fail, CriterionState.Pass),
            ],
            delta.Changes);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unchanged_run_has_no_changes()
    {
        var findings = new[] { A.Finding("SV-1", CriterionState.Pass) };

        var delta = RunDelta.Between(RunRecord.From(_repo, _earlier, 2, findings), RunRecord.From(_repo, _now, 2, findings));

        Assert.Empty(delta.Changes);
        Assert.Equal(2, delta.LevelBefore);
    }
}
