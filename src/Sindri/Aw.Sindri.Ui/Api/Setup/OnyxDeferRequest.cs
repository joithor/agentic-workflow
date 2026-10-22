namespace Aw.Sindri.Ui.Api.Setup;

/// <summary>POST /sindri/onyx/defer's body (setup-once spec §5.6 DeferOnyx, desk): "Not now", or the reverse.</summary>
/// <param name="Deferred">True to set Onyx up later; false to take that back.</param>
internal sealed record OnyxDeferRequest(bool Deferred);
