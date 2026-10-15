using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A budget line in tokens (E-23).</summary>
/// <param name="Scope">The scope, state.</param>
/// <param name="Used">Used.</param>
/// <param name="Limit">Limit.</param>
/// <param name="Instance">The instance-wide figures.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusBudget(
    string Scope,
    long Used,
    long? Limit,
    BudgetFigures Instance);
