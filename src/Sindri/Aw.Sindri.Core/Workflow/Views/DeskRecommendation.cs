using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>What the step before a stop recommends the person chooses, read from 'fact:recommendation'.</summary>
/// <param name="Outcome">One of the desk step's outcomes.</param>
/// <param name="Why">The reason in plain words, or null (left out of the JSON).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeskRecommendation(
    string Outcome,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Why = null);
