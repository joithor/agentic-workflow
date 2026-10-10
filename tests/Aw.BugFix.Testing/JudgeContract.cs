using Aw.BugFix.Core;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;
using Xunit;

namespace Aw.BugFix.Testing;

/// <summary>The <see cref="IJudge"/> contract, for the scripted fake today and the judge CLI adapter once judge publishes its C# contract.</summary>
public abstract class JudgeContract
{
    private static readonly ResolutionCheckInput _input = new("b", "e", "a", "r", CheckKind.Test, "s", BeforePassed: false, AfterPassed: true, "stat", "diff");

    /// <summary>A decision other than an escalation carries the stored decision's id.</summary>
    /// <param name="decision">The decision judge makes.</param>
    [Theory]
    [InlineData(JudgeDecision.Resolved)]
    [InlineData(JudgeDecision.Partial)]
    [InlineData(JudgeDecision.Unresolved)]
    public void A_decision_carries_its_id(JudgeDecision decision)
    {
        var answer = Ask(Decides(decision));

        Expect.Equal(decision, answer.Value.Decision, "Equal(decision, answer.Value.Decision)");
        Expect.That(answer.Value.DecisionId is not null, "NotNull(answer.Value.DecisionId)");
    }

    /// <summary>When judge gives no usable verdict the answer is AW-BUGFIX-016.</summary>
    [Fact]
    public void No_usable_verdict_is_AW_BUGFIX_016() =>
        Expect.Equal(BugFixErrors.JudgeUnavailable.Code, Ask(Fails()).Error.Code, "Equal(BugFixErrors.JudgeUnavailable.Code, Ask(Fails()).Error.Code)");

    /// <summary>A judge that makes <paramref name="decision"/>.</summary>
    /// <param name="decision">The decision.</param>
    /// <returns>The port.</returns>
    protected abstract IJudge Decides(JudgeDecision decision);

    /// <summary>A judge that fails.</summary>
    /// <returns>The port.</returns>
    protected abstract IJudge Fails();

    private static Result<JudgeAnswer> Ask(IJudge judge) => judge.ResolutionCheck(_input);
}
