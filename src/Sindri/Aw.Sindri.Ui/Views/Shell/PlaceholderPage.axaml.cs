using Avalonia;
using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>A page the API doesn't serve yet: its title and "not served yet".</summary>
internal sealed partial class PlaceholderPage : UserControl
{
    /// <summary>The page's title.</summary>
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<PlaceholderPage, string>(nameof(Title), string.Empty);

    /// <summary>What the page says under its title.</summary>
    public static readonly StyledProperty<string> NoteProperty = AvaloniaProperty.Register<PlaceholderPage, string>(nameof(Note), Labels.NotServedYet);

    public PlaceholderPage()
    {
        InitializeComponent();
    }

    /// <summary>What the page says under its title: "not served yet" or "arrives with &lt;phase&gt;".</summary>
    public string Note
    {
        get => GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    /// <summary>The page's title (a label).</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
