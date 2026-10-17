using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The checks the engine runs on a desk report before it accepts it (API spec §3.8 'DeskForm'): one per output the Step requires
/// on the outcome the form's complete command reports (its first declared; any outcome when it declares none). A check's id is
/// the output's name without 'fact:', a dot and the output's declared type ('output.file'); an output with no declared type is
/// checked for being present ('output.present').
/// </summary>
internal static class DeskChecks
{
    /// <summary>Whether the engine checks <paramref name="output"/> on a report for <paramref name="task"/>.</summary>
    /// <param name="task">The open desk task.</param>
    /// <param name="output">One of the Step's declared outputs.</param>
    /// <returns>True when the output is required on the outcome the form's command reports and the engine doesn't fill it.</returns>
    public static bool Applies(DeskTask task, OutputSpec output) =>
        task.Outcomes.Count == 0 ? StepOutputs.CallerWritesSomewhere(output) : StepOutputs.CallerWritesOn(output, task.Outcomes[0]);

    /// <summary>The id of the check on <paramref name="fact"/>.</summary>
    /// <param name="interpreted">The recipe, which declares the output's type.</param>
    /// <param name="fact">The output fact.</param>
    /// <returns>'&lt;name&gt;.&lt;type&gt;', or '&lt;name&gt;.present' when the recipe declares no type.</returns>
    public static string Id(InterpretedRecipe interpreted, FactKey fact)
    {
        var declared = interpreted.Recipe.Outputs.FirstOrDefault(o => o.Key == fact)?.Type;
        return $"{SubjectRefs.Bare(fact.Value)}.{(declared is null ? "present" : FactTypes.Of(declared))}";
    }
}
