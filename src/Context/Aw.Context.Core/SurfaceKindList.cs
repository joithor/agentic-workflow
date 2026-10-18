namespace Aw.Context.Core;

/// <summary>The one list of surface kinds. It sits at the root so the settings check (Engine) and the map rules (Graph) both read it without depending on each other.</summary>
public static class SurfaceKindList
{
    /// <summary>The surface kinds: 'surface:&lt;kind&gt;/&lt;name&gt;'.</summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "screen", "pane", "dialog", "command", "keybinding", "setting", "menu", "route", "api", "job", "notification", "permission", "report", "flag",
    };
}
