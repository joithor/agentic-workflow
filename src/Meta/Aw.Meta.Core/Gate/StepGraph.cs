using Aw.Kernel;
using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Gate;

/// <summary>The steps a gate run selected and what each waits for.</summary>
public sealed class StepGraph
{
    private readonly IReadOnlyList<IReadOnlyList<int>> _waits;

    private StepGraph(IReadOnlyList<StepConfig> steps, IReadOnlyList<IReadOnlyList<int>> waits)
    {
        Steps = steps;
        _waits = waits;
    }

    /// <summary>The steps, in config order.</summary>
    public IReadOnlyList<StepConfig> Steps { get; }

    /// <summary>The indexes of the steps that step <paramref name="index"/> waits for.</summary>
    /// <param name="index">The step's index in <see cref="Steps"/>.</param>
    /// <returns>The indexes.</returns>
    public IReadOnlyList<int> DependenciesOf(int index) => _waits[index];

    /// <summary>
    /// Resolves each step's dependencies: its After list, else the inferred ones. Inferred: a step that builds (dotnet build, or
    /// dotnet test/publish without --no-build) waits for the previous builder, so builders chain; any other heavy step or step
    /// that runs dotnet waits for every builder in the selection; the rest wait for nothing.
    /// </summary>
    /// <param name="selected">The steps to run, in config order.</param>
    /// <param name="configured">Every step name the gate configures (a name not selected is ignored; one not configured is an error).</param>
    /// <returns>The graph, or AW-META-003 for an unknown name, a step waiting for itself, or a cycle.</returns>
    public static Result<StepGraph> Build(IReadOnlyList<StepConfig> selected, IReadOnlyCollection<string> configured)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(configured);
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < selected.Count; i++)
        {
            index[selected[i].Name] = i;
        }

        var builders = Enumerable.Range(0, selected.Count).Where(i => Builds(selected[i].Run)).ToList();
        var waits = new List<IReadOnlyList<int>>(selected.Count);
        for (var i = 0; i < selected.Count; i++)
        {
            var step = selected[i];
            if (step.After is null)
            {
                waits.Add(Inferred(selected, builders, i));
                continue;
            }

            var named = Named(step, index, configured);
            if (named.IsError)
            {
                return named.Error;
            }

            waits.Add(named.Value);
        }

        return FindCycle(selected, waits) is { } looping
            ? MetaErrors.ConfigInvalid.With($"gate steps wait for each other in a cycle through '{looping}'")
            : new StepGraph(selected, waits);
    }

    private static Result<IReadOnlyList<int>> Named(StepConfig step, Dictionary<string, int> index, IReadOnlyCollection<string> configured)
    {
        var named = new List<int>();
        foreach (var name in step.After ?? [])
        {
            if (name == step.Name)
            {
                return MetaErrors.ConfigInvalid.With($"gate step '{step.Name}' waits for itself");
            }

            if (index.TryGetValue(name, out var at))
            {
                named.Add(at);
            }
            else if (!configured.Contains(name, StringComparer.Ordinal))
            {
                return MetaErrors.ConfigInvalid.With($"gate step '{step.Name}' waits for '{name}', which is not a configured step");
            }
        }

        return named;
    }

    private static IReadOnlyList<int> Inferred(IReadOnlyList<StepConfig> selected, List<int> builders, int i)
    {
        if (builders.Contains(i))
        {
            var before = builders.LastOrDefault(b => b < i, -1);
            return before < 0 ? [] : [before];
        }

        var step = selected[i];
        return step.Heavy || Mentions(step.Run) ? [.. builders] : [];
    }

    private static bool Mentions(IReadOnlyList<string> run) => run.Contains("dotnet", StringComparer.Ordinal);

    private static bool Builds(IReadOnlyList<string> run) =>
        run.Count > 1 && run[0] == "dotnet"
        && (run.Contains("build", StringComparer.Ordinal)
            || ((run.Contains("test", StringComparer.Ordinal) || run.Contains("publish", StringComparer.Ordinal)) && !run.Contains("--no-build", StringComparer.Ordinal)));

    private static string? FindCycle(IReadOnlyList<StepConfig> steps, IReadOnlyList<IReadOnlyList<int>> waits)
    {
        var state = new int[steps.Count];
        for (var i = 0; i < steps.Count; i++)
        {
            if (Visit(i, waits, state))
            {
                return steps[i].Name;
            }
        }

        return null;
    }

    private static bool Visit(int node, IReadOnlyList<IReadOnlyList<int>> waits, int[] state)
    {
        if (state[node] == 2)
        {
            return false;
        }

        if (state[node] == 1)
        {
            return true;
        }

        state[node] = 1;
        if (waits[node].Any(next => Visit(next, waits, state)))
        {
            return true;
        }

        state[node] = 2;
        return false;
    }
}
