using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.Core.Criteria;

/// <summary>The readiness criteria for this repo's C# template (scope "csharp-template"): the R0 baseline's 34
/// criteria (docs/readiness/baseline-2026-10.md), same ids and levels, each checked against the C# build surface —
/// src/, tests/, the root build files, the CI .NET job, AGENTS.md and the rules — instead of the TS apps.</summary>
public static class TemplateReadiness
{
    /// <summary>Every criterion, in report order.</summary>
    public static IReadOnlyList<Criterion> Criteria => throw new NotSupportedException("red: TemplateReadiness.Criteria");

    /// <summary>Evaluates every criterion.</summary>
    /// <param name="tree">The checkout.</param>
    /// <param name="host">What the repo host reports.</param>
    /// <param name="now">The run's time (for windows such as releases in the last 90 days).</param>
    /// <returns>One finding per criterion, in report order.</returns>
    public static IReadOnlyList<Finding> Evaluate(IRepoTree tree, HostFacts host, DateTimeOffset now)
        => throw new NotSupportedException("red: TemplateReadiness.Evaluate");
}
