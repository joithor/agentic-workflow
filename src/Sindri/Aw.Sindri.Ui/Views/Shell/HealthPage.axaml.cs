using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The Health page: binds to the health view model, holds no logic.</summary>
internal sealed partial class HealthPage : UserControl
{
    public HealthPage()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
