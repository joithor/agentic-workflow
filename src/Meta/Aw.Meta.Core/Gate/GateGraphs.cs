using Aw.Kernel;
using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Gate;

/// <summary>Builds the step graph of the scopes a gate selected.</summary>
public static class GateGraphs
{
    /// <summary>The steps of the scopes, in order, as a graph.</summary>
    /// <param name="scopes">The selected scope names.</param>
    /// <param name="gate">The gate config.</param>
    /// <returns>The graph, or AW-META-003 for a duplicate step name, an unknown wait or a cycle.</returns>
    public static Result<StepGraph> Build(IReadOnlyList<string> scopes, GateConfig gate)
    {
        var configured = scopes.SelectMany(scope => gate.Scopes[scope].Steps).ToList();
        if (configured.GroupBy(s => s.Name, StringComparer.Ordinal).FirstOrDefault(g => g.Skip(1).Any()) is { } twice)
        {
            return MetaErrors.ConfigInvalid.With($"two gate steps are named '{twice.Key}'; step names must be unique across the selected scopes");
        }

        return StepGraph.Build(configured, [.. gate.Scopes.Values.SelectMany(scope => scope.Steps).Select(step => step.Name)]);
    }
}
