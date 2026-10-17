using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Facts;

namespace Aw.Sindri.Ui.Views.Facts;

/// <summary>A text fact's viewer: Copy puts the line on the clipboard.</summary>
internal sealed partial class TextFactView : UserControl
{
    public TextFactView()
    {
        InitializeComponent();
    }

    private void OnCopy(object? sender, RoutedEventArgs e) => _ = (DataContext as TextFactViewer)?.Copy(CancellationToken.None);
}
