namespace Aw.Architecture.Tests.Fixtures.Ui.ViewModels;

// ARCH-UI-001 red: a view model holding an Avalonia control.
internal sealed class LeakyViewModel
{
    public Avalonia.Controls.TextBlock? Label { get; set; }
}
