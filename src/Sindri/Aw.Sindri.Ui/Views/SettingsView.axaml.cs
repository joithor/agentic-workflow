using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Aw.Sindri.Ui.Views;

/// <summary>The Settings page: binds to the settings view model, holds no logic.</summary>
internal sealed partial class SettingsView : UserControl
{
    public SettingsView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
