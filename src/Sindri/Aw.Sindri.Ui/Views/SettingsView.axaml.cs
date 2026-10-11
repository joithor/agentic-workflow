using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Aw.Sindri.Ui.Views;

/// <summary>The Settings page: binds to the settings view model, holds no logic.</summary>
[SuppressMessage("Design", "MA0182:Internal type is apparently never used", Justification = "U0 Unit D: the shell hosts it as its Settings page when the integrator wires it; until then only the view tests use it.")]
internal sealed partial class SettingsView : UserControl
{
    public SettingsView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
