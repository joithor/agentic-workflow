using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A state's Step (E-20).</summary>
/// <param name="Kind">The Step kind.</param>
/// <param name="Ref">The block reference.</param>
/// <param name="Declaration">What it does.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StepDeclaration(
    string Kind,
    string Ref,
    string Declaration);
