using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Components;

#pragma warning disable MA0182 // a stub until the U8 pages place the component
/// <summary>The Attach component (red stub).</summary>
internal sealed partial class AttachButton : UserControl
{
    public AttachButton()
    {
        InitializeComponent();
    }

    /// <summary>True for the compact size (row actions); false for the normal size (frame headers).</summary>
    public bool IsCompact { get; set; }
}
#pragma warning restore MA0182
