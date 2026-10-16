namespace Aw.Sindri.Ui.ViewModels.Components;

/// <summary>One entry of the chevron menu.</summary>
/// <param name="Action">What choosing it asks for.</param>
/// <param name="Label">The visible label.</param>
/// <param name="Name">The accessible name, which says what it acts on.</param>
internal sealed record AttachMenuItem(AttachAction Action, string Label, string Name);
