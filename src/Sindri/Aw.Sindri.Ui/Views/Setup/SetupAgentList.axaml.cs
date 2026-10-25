using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Setup;

#pragma warning disable MA0182 // why: the step row places it in XAML, which the analyzer can't see
/// <summary>The agents step's list: it draws the step view model and holds no logic.</summary>
internal sealed partial class SetupAgentList : UserControl
{
    public SetupAgentList()
    {
        InitializeComponent();
    }
}
#pragma warning restore MA0182
