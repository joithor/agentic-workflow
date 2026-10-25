namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>POST /sindri/workflow/instances' body (StartInstance, desk, engine P6): the item is the subject, and the facts are keyed by the recipe's input keys.</summary>
/// <param name="Recipe">The recipe id (the approved latest), or 'id@hash'.</param>
/// <param name="Item">The item the workflow runs for.</param>
/// <param name="NextAttempt">True to start the attempt after the latest one (a finished one); false for the latest, or the first.</param>
/// <param name="Facts">The input facts by key ('fact:spec').</param>
internal sealed record StartInstanceRequest(string Recipe, string Item, bool NextAttempt, IReadOnlyDictionary<string, StartFact> Facts);
