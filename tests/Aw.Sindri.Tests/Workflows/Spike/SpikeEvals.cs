using System.Text.Json;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>The shipped 'workflow.step' recipe (read with P1's reader) and its eval cases.</summary>
internal static class SpikeEvals
{
    /// <summary>recipes/sindri/workflow.step.yaml, normalized by YamlRecipeReader, projected for the interpreter.</summary>
    public static SpikeRecipe WorkflowStep { get; } = Read();

    /// <summary>The trigger lines of recipes/sindri/evals/workflow.step/<paramref name="name"/>.jsonl.</summary>
    /// <param name="name">'happy', 'no-progress' or 'rounds'.</param>
    /// <returns>The lines after the start line.</returns>
    public static IReadOnlyList<SpikeEvalLine> Case(string name)
    {
        var lines = ShippedRecipes.Read($"sindri/evals/workflow.step/{name}.jsonl").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return [.. lines.Skip(1).Select((text, i) => Line(text, i + 2))];
    }

    private static SpikeEvalLine Line(string text, int number)
    {
        using var json = JsonDocument.Parse(text);
        var trigger = json.RootElement.GetProperty("trigger");
        var expect = json.RootElement.GetProperty("expect");
        var outputs = trigger.TryGetProperty("outputs", out var o)
            ? o.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        return new SpikeEvalLine(
            number,
            new SpikeReport(trigger.GetProperty("outcome").GetString()!, outputs),
            expect.GetProperty("state").GetString()!,
            expect.TryGetProperty("park", out var park) ? park.GetString() : null);
    }

    private static SpikeRecipe Read()
    {
        var reading = new YamlRecipeReader(SliceHarness.Json).Read(ShippedRecipes.Read("sindri/workflow.step.yaml"), "workflow.step.yaml");
        var recipe = reading.Document?.Recipe ?? throw new InvalidOperationException("workflow.step.yaml didn't read: " + string.Join("; ", reading.Findings));
        return SpikeRecipe.From(recipe);
    }
}
