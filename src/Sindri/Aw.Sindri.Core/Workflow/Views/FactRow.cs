using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>One input or output the recipe declares (E-20).</summary>
/// <param name="Key">The key.</param>
/// <param name="Label">The label.</param>
/// <param name="Type">The type.</param>
/// <param name="Direction">Input or output.</param>
/// <param name="From">The state that produces it.</param>
/// <param name="Status">How far it is.</param>
/// <param name="Summary">The one-line summary.</param>
/// <param name="At">When produced.</param>
/// <param name="By">Who produced it, an open actor string.</param>
/// <param name="Ref">The per-type handle.</param>
/// <param name="Progress">Evidence progress.</param>
/// <param name="Pending">What an open desk asks for.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactRow(
    string Key,
    string Label,
    FactType Type,
    FactDirection Direction,
    string? From,
    FactStatus Status,
    string Summary,
    DateTimeOffset? At,
    string? By,
    FactRef? Ref,
    Progress? Progress,
    FactPending? Pending);
