namespace Aw.Sindri.Features.OnyxEnsure;

/// <summary>What `sindri serve` asks once the control plane is up and Onyx is Sindri's: bring the managed Onyx up if it is not running (spec §4.2). It never fetches, pulls or bootstraps.</summary>
public sealed record OnyxEnsureRequest;
