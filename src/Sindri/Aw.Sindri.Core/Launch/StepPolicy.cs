namespace Aw.Sindri.Core.Launch;

/// <summary>
/// A Step's policy as the engine wrote it from the recipe's W9 (launcher L-17): one value the catalog entry's policy form turns into
/// arguments. 'claude-settings-file' passes <see cref="Path"/>; 'codex-overrides' passes each of <see cref="Overrides"/> as '-c'.
/// </summary>
/// <param name="Path">The per-launch policy file (0600), written by the engine.</param>
/// <param name="Overrides">The same policy as 'key=value' config overrides, in the order they go on the command line.</param>
public sealed record StepPolicy(string Path, IReadOnlyList<string> Overrides);
