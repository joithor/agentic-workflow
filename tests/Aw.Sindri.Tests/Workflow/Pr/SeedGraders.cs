namespace Aw.Sindri.Tests.Workflow.Pr;

/// <summary>The deterministic 'check:' graders of the four babysitter and docs-sync seeds (PB spec Appendix B.2).</summary>
internal static class SeedGraders
{
    /// <summary>Grades one run of a seed.</summary>
    /// <param name="step">The seed's Step id.</param>
    /// <param name="run">What the run left behind.</param>
    /// <param name="fixture">What the case knows about its fixture.</param>
    /// <returns>One line per failed check; empty when the run passes every check.</returns>
    internal static IReadOnlyList<string> Check(string step, SeedRun run, SeedCase fixture) => throw new NotSupportedException();
}
