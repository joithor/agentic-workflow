namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// What the seen and pulse view models need of a needs-you row: its id and urgency. A UI input record of the app's own, so
/// they don't depend on the API's row (which arrives with the contract unit); the list view model maps its rows to it.
/// </summary>
/// <param name="Id">The row's id, the key of its seen state.</param>
/// <param name="Urgency">The API's urgency.</param>
internal sealed record NeedsYouEntry(string Id, NeedsYouUrgency Urgency);
