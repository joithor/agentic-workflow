using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Setup;

#pragma warning disable MA0182 // why: the checklist's row template places it in XAML, which the analyzer can't see
/// <summary>One step of the checklist: it draws the step view model and holds no logic.</summary>
internal sealed partial class SetupStepRow : UserControl
{
    public SetupStepRow()
    {
        InitializeComponent();
    }
}
#pragma warning restore MA0182
