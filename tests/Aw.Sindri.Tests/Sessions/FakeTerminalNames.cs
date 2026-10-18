using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The terminals.json rows the fixture tree needs, or the rows a test passes.</summary>
/// <param name="rows">The rows; the fixture tree's four terminals when omitted.</param>
internal sealed class FakeTerminalNames(IReadOnlyList<TerminalName>? rows = null) : ITerminalNames
{
    public IReadOnlyList<TerminalName> All { get; } = rows ??
    [
        new("kitty", ["kitty"]), new("wezterm", ["wezterm-gui"]), new("gnome-terminal", ["gnome-terminal-server"]), new("xterm", ["xterm"]),
    ];
}
