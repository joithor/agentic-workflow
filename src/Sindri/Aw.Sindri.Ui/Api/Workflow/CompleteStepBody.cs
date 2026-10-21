namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>
/// The body of POST /sindri/workflow/instances/{id}/runs/{run}/complete (engine spec rev 8.1 §6.3; desk): the report for an open desk run.
/// The instance and the run are in the route as well; the route wins.
/// </summary>
/// <param name="Instance">The instance's id.</param>
/// <param name="Run">The open run's token, as the instance's desk form serves it.</param>
/// <param name="Outcome">One of the step's declared outcomes.</param>
/// <param name="Outputs">Each output fact's content by its key ("fact:answers"), sent as base64.</param>
internal sealed record CompleteStepBody(string Instance, string Run, string Outcome, IReadOnlyDictionary<string, byte[]> Outputs);
