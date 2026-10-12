namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the command palette and the shortcuts sheet (§8, §12.4).</summary>
internal static partial class Labels
{
    /// <summary>The palette's accessible name.</summary>
    internal const string Palette = "Command palette";

    /// <summary>The palette's section of catalog actions.</summary>
    internal const string PaletteActions = "Actions";

    /// <summary>The palette's section of pages.</summary>
    internal const string PaletteGoTo = "Go to";

    /// <summary>A ready entry's status.</summary>
    internal const string Ready = "ready";

    /// <summary>The palette's line when nothing matches.</summary>
    internal const string NoMatches = "Nothing matches";

    /// <summary>The shortcuts sheet's title and accessible name.</summary>
    internal const string Shortcuts = "Keyboard shortcuts";

    /// <summary>The shortcuts sheet's closing hint.</summary>
    internal const string EscCloses = "Esc closes";

    /// <summary>A cli-only entry's status: "Copy: sindri doctor".</summary>
    /// <param name="cli">The catalog's CLI.</param>
    /// <returns>The words.</returns>
    internal static string CopyCli(string cli) => "Copy: " + cli;

    /// <summary>§8's fixed keys and what they do, in the table's order (the `?` sheet).</summary>
    internal static IReadOnlyList<(string Keys, string Action)> FixedKeys { get; } =
    [
        ("⌘K / Ctrl+K", Palette),
        ("G then N H Q R W D S .", "Go to Needs you, Home, Queue, Running, With you, Decisions, Services, Health"),
        ("J / K", "Next / previous row"),
        ("Enter / Esc", "Open the detail sheet / close the topmost layer"),
        ("O", "Open with the resolved target; Shift+O opens the target menu"),
        ("A", "The row's primary action"),
        ("Shift+A", "Mark all seen"),
        ("X / Shift+X", "Reject or decline / cancel the instance"),
        ("T / B", "Take over / hand back"),
        ("Y", "Copy the selected row's CLI"),
        ("C / R", "Create item / file a request"),
        ("/", "Filter the current list"),
        ("?", "Shortcuts sheet"),
        ("⌘,", Settings),
        ("F6 / Shift+F6", "Next / previous region"),
    ];
}
