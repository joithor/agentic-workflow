using System.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The 'workflow complete' line a desk task shows (API spec §3.8 'desk runtime'): one formatter for the status and 'next' lines, the
/// allowed command and the refusal that points a session at the human, so they all list the same outputs.
/// </summary>
public static class CompleteCommand
{
    /// <summary>The command reporting <paramref name="outcome"/>, with an '--output &lt;fact&gt;=&lt;file&gt;' flag for each output the caller has to write.</summary>
    /// <param name="address">The instance address.</param>
    /// <param name="run">The open run.</param>
    /// <param name="outcome">The outcome the line reports; null shows '&lt;outcome&gt;' and no outputs.</param>
    /// <param name="outputs">The Step's declared outputs.</param>
    /// <returns>The line; engine-filled outputs are left out, the engine writes them.</returns>
    public static string Line(string address, RunToken run, OutcomeName? outcome, IEnumerable<OutputSpec> outputs)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        var line = new StringBuilder($"sindri workflow complete {address} --run {run.Value} --outcome ");
        if (outcome is not { } reported)
        {
            return line.Append("<outcome>").ToString();
        }

        _ = line.Append(reported.Value);
        foreach (var output in outputs.Where(o => StepOutputs.RequiredOn(o, reported) && o.EngineFilled != true))
        {
            _ = line.Append(" --output ").Append(output.Fact.Value).Append("=<file>");
        }

        return line.ToString();
    }
}
