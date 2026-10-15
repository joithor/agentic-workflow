using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Gate;

/// <summary>Builds the argument vectors the gate runs.</summary>
public static class GateCommand
{
    private const string TestPlaceholder = "{test}";

    /// <summary>The step's command, through the heavy-job wrapper when the step is heavy and a wrapper is installed.</summary>
    /// <param name="step">The step.</param>
    /// <param name="heavy">The wrapper program, or null when none is installed.</param>
    /// <returns>The argument vector.</returns>
    public static IReadOnlyList<string> For(StepConfig step, string? heavy)
    {
        ArgumentNullException.ThrowIfNull(step);
        return step.Heavy && heavy is not null ? [heavy, .. step.Run] : step.Run;
    }

    /// <summary>The command that runs one test of the step: the step's rerun template, else its own command with a method filter (dotnet test steps only).</summary>
    /// <param name="step">The step.</param>
    /// <param name="test">The test filter.</param>
    /// <param name="heavy">The wrapper program, or null when none is installed.</param>
    /// <returns>The argument vector, or null when the step has no way to run one test.</returns>
    public static IReadOnlyList<string>? Rerun(StepConfig step, string test, string? heavy)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(test);
        IReadOnlyList<string>? argv = step.Rerun is { Count: > 0 } template
            ? [.. template.Select(word => word.Replace(TestPlaceholder, test, StringComparison.Ordinal))]
            : IsDotnetTest(step.Run) ? [.. step.Run, "--filter-method", "*" + test + "*"] : null;
        return argv is null ? null : step.Heavy && heavy is not null ? [heavy, .. argv] : argv;
    }

    private static bool IsDotnetTest(IReadOnlyList<string> run) =>
        run.Count > 1 && run[0] == "dotnet" && run.Contains("test", StringComparer.Ordinal);
}
