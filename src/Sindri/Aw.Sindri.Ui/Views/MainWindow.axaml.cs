using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Aw.Sindri.Ui.Views;

/// <summary>The main window: binds to the main view model, holds no logic.</summary>
internal sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
