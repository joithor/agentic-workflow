using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The terminals.json rows the fixture tree needs.</summary>
internal sealed class FakeTerminalNames : ITerminalNames
{
    public IReadOnlyList<TerminalName> All { get; } =
    [
        new("kitty", ["kitty"]), new("wezterm", ["wezterm-gui"]), new("gnome-terminal", ["gnome-terminal-server"]), new("xterm", ["xterm"]),
    ];
}
