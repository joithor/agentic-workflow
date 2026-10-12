namespace Aw.Sindri.Ui.Testing;

/// <summary>A request the canned handler saw: its method, path and the caller headers (null when absent).</summary>
/// <param name="Path">The URL path.</param>
/// <param name="Caller">X-Sindri-Caller.</param>
/// <param name="Client">X-Sindri-Client.</param>
/// <param name="Terminal">X-Sindri-Terminal.</param>
/// <param name="Origin">Origin.</param>
/// <param name="Token">X-Sindri-Token.</param>
/// <param name="After">X-Sindri-After.</param>
/// <param name="Method">The HTTP method.</param>
internal sealed record SeenRequest(string Path, string? Caller, string? Client, string? Terminal, string? Origin, string? Token = null, string? After = null, string Method = "GET");
