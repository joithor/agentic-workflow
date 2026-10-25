using System.Collections.Concurrent;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Tests.Workflows.Inputs;

/// <summary>What the probe inputs do and what they saw: shared by the two input classes through DI.</summary>
internal sealed class InputProbe
{
    /// <summary>The text the inputs plant, which no stored surface may hold.</summary>
    public const string Planted = "PLANTED-SECTION-TEXT-7f3a";

    /// <summary>One line per call: '{input} {step}'.</summary>
    public ConcurrentQueue<string> Calls { get; } = new();

    /// <summary>What a call returns; throws when it is null.</summary>
    public Func<StepInputContext, InjectionSections>? Behavior { get; set; } = static _ => new InjectionSections(new Dictionary<string, string>(StringComparer.Ordinal) { ["notes"] = Planted });

    public InjectionSections Fill(StepInputContext context)
    {
        Calls.Enqueue($"{context.Input.Value} {context.Step.Value}");
        return Behavior?.Invoke(context) ?? throw new InvalidOperationException("the probe input was told to throw");
    }

    /// <summary>A result of one section 'notes' and a typed result.</summary>
    public static InjectionSections WithResult(string section) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal) { [section] = Planted }, JsonDocument.Parse("{\"count\":2}").RootElement.Clone());
}
