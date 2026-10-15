using Aw.Context.Core;
using Aw.Sindri.Core;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The exit codes of the context verbs (context spec §8.6): 0 ok, 1 attention, 2 a tool error, 75 the heavy-job lock stayed busy.</summary>
public sealed class ContextExitTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_busy_heavy_lock_exits_75() => Assert.Equal(75, ContextExit.For(ContextErrors.HeavyLockBusy.With("held by 77"), toolExit: 2));

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Attention))]
    public void Attention_codes_exit_1(string code)
    {
        var error = ContextErrors.All.Single(e => e.Code == code).With("x");

        Assert.Equal(1, ContextExit.For(error, toolExit: 2));
    }

    public static TheoryData<string> Attention() => [.. new[]
    {
        ContextErrors.LabelingUnavailable, ContextErrors.ReviewFileNoMatch, ContextErrors.NoDraftMap, ContextErrors.RepoOutsideScope,
        ContextErrors.PromoteRefused, ContextErrors.ReviewTargetStale, ContextErrors.UnknownAnchor, ContextErrors.NoMap,
    }.Select(e => e.Code)];

    [Fact, Trait("Lane", "unit")]
    public void Extraction_unreadable_048_and_other_tool_errors_exit_2_while_not_running_and_a_refused_caller_exit_1()
    {
        Assert.Equal(2, ContextExit.For(ContextErrors.ExtractionUnreadable.With("no repo"), toolExit: 2));
        Assert.Equal(2, ContextExit.For(ContextErrors.StoreUnavailable.With("disk"), toolExit: 2));
        Assert.Equal(1, ContextExit.For(SindriErrors.NotRunning.With("down"), toolExit: 1));
        Assert.Equal(1, ContextExit.For(SindriErrors.AuthRefused.With("no"), toolExit: 2));
        Assert.Equal(1, ContextExit.For(SindriErrors.ApiNoCode.With("x"), toolExit: 1));
    }
}
