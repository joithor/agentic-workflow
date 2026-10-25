namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>POST /sindri/workflow/instances' answer: the instance (in the engine's shape; only its id and address are read here) and whether it already existed.</summary>
/// <param name="Instance">The started, or already running, or finished instance.</param>
/// <param name="Existing">True when the item already had this recipe's attempt.</param>
/// <param name="Finished">True when that attempt has ended: a next attempt has to be asked for.</param>
internal sealed record StartedView(StartedInstanceView Instance, bool Existing, bool Finished);
