namespace Aw.Sindri.Ui.Platform;

/// <summary>What the tray shows. Every text is the app's (its labels); the head escapes it for its platform's menus.</summary>
/// <param name="State">The icon.</param>
/// <param name="Tooltip">The icon's tooltip.</param>
/// <param name="OpenLabel">The "Open Sindri" item.</param>
/// <param name="QuitLabel">The "Quit" item.</param>
public sealed record TrayView(TrayState State, string Tooltip, string OpenLabel, string QuitLabel)
{
    /// <summary>The most needs-you rows the menu lists (UI spec §5).</summary>
    public const int MaxRows = 5;

    /// <summary>The count drawn on an <see cref="TrayState.Attention"/> or <see cref="TrayState.Calm"/> icon (9 and over draw as "9+").</summary>
    public int Count { get; init; }

    /// <summary>The needs-you rows, in the API's order; the menu lists the first <see cref="MaxRows"/>.</summary>
    public IReadOnlyList<TrayRow> Rows { get; init; } = [];

    /// <summary>The disabled "Running: N" item after "Open Sindri"; empty for none.</summary>
    public string RunningLabel { get; init; } = "";

    /// <summary>The rows the menu lists: at most <see cref="MaxRows"/>.</summary>
    public IEnumerable<TrayRow> MenuRows => Rows.Take(MaxRows);
}
