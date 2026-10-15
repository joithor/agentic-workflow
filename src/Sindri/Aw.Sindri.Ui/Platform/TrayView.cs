namespace Aw.Sindri.Ui.Platform;

/// <summary>What the tray shows. Every text is the app's (its labels); the head escapes it for its platform's menus.</summary>
/// <param name="State">The icon.</param>
/// <param name="Tooltip">The icon's tooltip.</param>
/// <param name="OpenLabel">The "Open Sindri" item.</param>
/// <param name="QuitLabel">The "Quit" item.</param>
public sealed record TrayView(TrayState State, string Tooltip, string OpenLabel, string QuitLabel);
