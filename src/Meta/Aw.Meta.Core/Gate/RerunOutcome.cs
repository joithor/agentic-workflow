namespace Aw.Meta.Core.Gate;

/// <summary>The result of running one test again.</summary>
/// <param name="Passed">Whether it passed.</param>
/// <param name="Failure">The failure text when it failed.</param>
public sealed record RerunOutcome(bool Passed, string? Failure = null);
