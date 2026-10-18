namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// Reads of <see cref="OutputSpec.RequiredOn"/> go through here, so the "every outcome" form (DT spec §8.1) is honoured everywhere:
/// the validator, the report validation and the eval read through it. The desk's views and the complete command read through it too.
/// </summary>
internal static class StepOutputs
{
    /// <summary>Whether <paramref name="spec"/> must be written when the Step reports <paramref name="outcome"/>.</summary>
    /// <param name="spec">One of the Step's declared outputs.</param>
    /// <param name="outcome">The reported outcome.</param>
    /// <returns>True for an "every outcome" output, or one whose required list names the outcome.</returns>
    public static bool RequiredOn(OutputSpec spec, Ids.OutcomeName outcome)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Every == true || spec.RequiredOn.Contains(outcome);
    }

    /// <summary>Whether <paramref name="spec"/> is required on some outcome: an "every outcome" output, or one whose required list isn't empty.</summary>
    /// <param name="spec">One of the Step's declared outputs.</param>
    /// <returns>True when the output is required on at least one outcome.</returns>
    public static bool RequiredSomewhere(OutputSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Every == true || spec.RequiredOn.Count > 0;
    }

    /// <summary>Whether the caller has to write <paramref name="spec"/> when the Step reports <paramref name="outcome"/>: required on it and not filled by the engine.</summary>
    /// <param name="spec">One of the Step's declared outputs.</param>
    /// <param name="outcome">The reported outcome.</param>
    /// <returns>True when the report must carry the output.</returns>
    public static bool CallerWritesOn(OutputSpec spec, Ids.OutcomeName outcome) => RequiredOn(spec, outcome) && spec.EngineFilled != true;

    /// <summary>Whether the caller has to write <paramref name="spec"/> on some outcome (the Step declares no outcomes to pick from).</summary>
    /// <param name="spec">One of the Step's declared outputs.</param>
    /// <returns>True when the output is required on some outcome and not filled by the engine.</returns>
    public static bool CallerWritesSomewhere(OutputSpec spec) => RequiredSomewhere(spec) && spec.EngineFilled != true;
}
