namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// Reads of <see cref="OutputSpec.RequiredOn"/> go through here, so the "every outcome" form (DT spec §8.1) is honoured everywhere:
/// the validator, the report validation and the eval read through it. The desk's views move onto it in DT-V2.
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
}
