namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>What one Step state reads and produces, as GET /workflow/instances/{id} serves it ('steps').</summary>
/// <param name="State">The state that runs the Step.</param>
/// <param name="Needs">The fact keys the Step reads.</param>
/// <param name="Produces">The facts leaving the state records, as the instance holds them so far.</param>
internal sealed record StateStep(string State, IReadOnlyList<string> Needs, IReadOnlyList<FactRef> Produces);
