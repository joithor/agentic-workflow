namespace Aw.Sindri.Features.OnyxDown;

/// <summary>What `sindri onyx down` did.</summary>
/// <param name="WasRunning">Some container of the project was running and has been stopped; false when nothing ran (already stopped, or never set up).</param>
public sealed record OnyxDownResponse(bool WasRunning);
