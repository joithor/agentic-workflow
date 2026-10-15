using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>One recipe input and whether the instance has it (E-20).</summary>
/// <param name="Key">The fact key.</param>
/// <param name="Label">The label.</param>
/// <param name="Kind">The subject adapter kind.</param>
/// <param name="Required">Whether it is required.</param>
/// <param name="Present">Whether the instance holds it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InputPresence(
    string Key,
    string Label,
    string Kind,
    bool Required,
    bool Present);
