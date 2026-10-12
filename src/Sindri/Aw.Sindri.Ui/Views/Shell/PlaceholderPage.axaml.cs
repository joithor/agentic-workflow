using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>A page the API doesn't serve yet: its title and "not served yet".</summary>
internal sealed partial class PlaceholderPage : UserControl
{
    /// <summary>The page's title.</summary>
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<PlaceholderPage, string>(nameof(Title), string.Empty);

    public PlaceholderPage()
    {
        InitializeComponent();
    }

    /// <summary>The page's title (a label).</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
