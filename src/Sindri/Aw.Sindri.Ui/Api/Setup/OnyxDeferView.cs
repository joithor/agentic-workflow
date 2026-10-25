namespace Aw.Sindri.Ui.Api.Setup;

/// <summary>The 200 of POST /sindri/onyx/defer.</summary>
/// <param name="Deferred">Whether Onyx is now set aside for later.</param>
internal sealed record OnyxDeferView(bool Deferred);
