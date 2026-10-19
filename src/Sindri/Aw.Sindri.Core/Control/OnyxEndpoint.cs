namespace Aw.Sindri.Core.Control;

/// <summary>The managed Onyx 'sindri serve' keeps beside itself, as control.json records it. Holds no secret.</summary>
/// <param name="Project">The compose project (`sindri-onyx-&lt;8 hex&gt;`).</param>
/// <param name="Url">http://127.0.0.1:&lt;port&gt;.</param>
/// <param name="State">`running`, `needs-setup` or `failed`.</param>
public sealed record OnyxEndpoint(string Project, string Url, string State);
