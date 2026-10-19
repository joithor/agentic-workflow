using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The short plain-text brief a launched agent session gets with its seed (engine P8 F13): where it is, what it reads, and the exact 'sindri workflow complete' command that ends its Step.</summary>
public static class AgentStepBrief
{
    /// <summary>Renders the brief; deterministic for the same run and descriptor.</summary>
    /// <param name="run">The run: the instance, the run token and the Step.</param>
    /// <param name="descriptor">The Step's descriptor, or null when the catalog doesn't know it.</param>
    /// <returns>The brief.</returns>
    public static string Render(AgentStepRun run, BlockDescriptor? descriptor) => throw new NotSupportedException();
}
