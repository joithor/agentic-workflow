namespace Aw.Sindri.Host.Commands;

/// <summary>What the registry's server verbs need from the CLI (engine spec §4.2, §6.3): the API client, the terminal, and the start rule (API spec §7).</summary>
/// <param name="Client">The control plane's API client, carrying this CLI's caller headers.</param>
/// <param name="Input">Where the typed action text is read from.</param>
/// <param name="EnsureRunning">Starts the control plane before a human's command, or names why not.</param>
internal sealed record RecipeDesk(ControlPlaneClient Client, TextReader Input, Func<CancellationToken, Task<Kernel.Error?>> EnsureRunning);
