using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aw.Sindri.Ui.Views.Queue;

/// <summary>An item row in Home's panels; a title click raises <see cref="ItemRow.OpenEvent"/> like the table row.</summary>
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
internal sealed partial class CompactItemRow : UserControl
{
    public CompactItemRow()
    {
        InitializeComponent();
        AddHandler(GotFocusEvent, (_, _) => RaiseEvent(new RoutedEventArgs(ItemRow.SelectEvent, this)));
    }

    private void OnOpen(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ItemRow.OpenEvent, this));
}
