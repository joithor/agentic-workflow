using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Visual;

/// <summary>What the visual gate decides on.</summary>
/// <param name="AfterScreenshot">The last passed step's screenshot.</param>
/// <param name="Baseline">The approved baseline, when there is one.</param>
/// <param name="HasFailedSteps">Whether any step failed or broke: such a run always gets a critique, never 'unchanged'.</param>
/// <param name="RunInstance">This run invocation (the verdict cache trusts an entry without an app build only within it).</param>
/// <param name="RunDirectory">Where the diff and crop images go.</param>
/// <param name="Context">The verdict-cache context.</param>
/// <param name="Cache">The verdict cache as loaded.</param>
public sealed record VisualGateInput(
    string AfterScreenshot,
    string? Baseline,
    bool HasFailedSteps,
    RunInstanceId RunInstance,
    string RunDirectory,
    VerdictCacheContext Context,
    VerdictCache Cache);
