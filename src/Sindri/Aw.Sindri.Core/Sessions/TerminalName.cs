namespace Aw.Sindri.Core.Sessions;

/// <summary>A terminal from config/launcher/terminals.json: its id and the process names it runs under (collector spec §6.1).</summary>
/// <param name="Name">The row id ('kitty').</param>
/// <param name="ProcessNames">The running process names ('wezterm-gui').</param>
public sealed record TerminalName(string Name, IReadOnlyList<string> ProcessNames);
