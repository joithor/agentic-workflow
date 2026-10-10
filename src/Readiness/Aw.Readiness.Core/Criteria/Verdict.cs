namespace Aw.Readiness.Core.Criteria;

/// <summary>A check's outcome before it is attached to its criterion.</summary>
/// <param name="State">The state.</param>
/// <param name="Evidence">What was measured.</param>
/// <param name="Unread">True when the check's data couldn't be read this run.</param>
internal sealed record Verdict(CriterionState State, string Evidence, bool Unread = false)
{
    internal static Verdict NotRead(string evidence) => new(CriterionState.Unknown, evidence, Unread: true);

    internal static Verdict Pass(string evidence) => new(CriterionState.Pass, evidence);

    internal static Verdict Fail(string evidence) => new(CriterionState.Fail, evidence);

    internal static Verdict Unknown(string evidence) => new(CriterionState.Unknown, evidence);

    internal static Verdict When(bool holds, string pass, string fail) => holds ? Pass(pass) : Fail(fail);
}
