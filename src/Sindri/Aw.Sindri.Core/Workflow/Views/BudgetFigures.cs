using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>Used over limit, in tokens.</summary>
/// <param name="Used">Used.</param>
/// <param name="Limit">Limit.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BudgetFigures(
    long Used,
    long? Limit);
