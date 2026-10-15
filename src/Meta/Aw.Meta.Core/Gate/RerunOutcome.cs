namespace Aw.Meta.Core.Gate;

/// <summary>The result of running one test again.</summary>
/// <param name="Passed">Whether it passed.</param>
/// <param name="Failure">The failure text when it failed.</param>
/// <param name="Absent">Whether no test matched the filter: on a plain-main tree, the test is not there.</param>
public sealed record RerunOutcome(bool Passed, string? Failure = null, bool Absent = false);
