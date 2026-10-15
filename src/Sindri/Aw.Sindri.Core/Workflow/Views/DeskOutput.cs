using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>One output a desk task asks for.</summary>
/// <param name="Key">The fact key.</param>
/// <param name="Type">The fact type.</param>
/// <param name="Required">Whether it is required.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeskOutput(
    string Key,
    FactType Type,
    bool Required);
