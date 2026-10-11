using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Home's panel frames; no bindings until their routes are served.</summary>
internal sealed partial class HomePage : UserControl
{
    public HomePage()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
