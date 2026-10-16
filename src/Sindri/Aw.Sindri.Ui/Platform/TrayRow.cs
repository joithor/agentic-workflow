namespace Aw.Sindri.Ui.Platform;

/// <summary>One needs-you row in the tray's menu (UI spec §5): picking it opens the app at that row; nothing runs.</summary>
/// <param name="Id">The row's id (the app opens needs-you/Id).</param>
/// <param name="Label">The row's menu text: kind, id, title and age, composed by the app.</param>
public sealed record TrayRow(string Id, string Label);
