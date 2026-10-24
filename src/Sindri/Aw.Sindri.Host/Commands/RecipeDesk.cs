namespace Aw.Sindri.Host.Commands;

/// <summary>
/// What the server verbs (the registry's and the workflow writes) need from the CLI (engine spec §4.2, §6.3): the API
/// client, the terminal, and the start rule (API spec §7).
/// </summary>
/// <param name="Client">The control plane's API client, carrying this CLI's caller headers.</param>
/// <param name="Input">Where the typed action text is read from.</param>
/// <param name="EnsureRunning">Starts the control plane before a human's command, or names why not.</param>
/// <param name="Environment">Reads a process variable (a launched session's AW_SINDRI_LAUNCH_SECRET); null reads none.</param>
internal sealed record RecipeDesk(ControlPlaneClient Client, TextReader Input, Func<CancellationToken, Task<Kernel.Error?>> EnsureRunning, Func<string, string?>? Environment = null);
