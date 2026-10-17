using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>The "Answer in the child" button of a row: pressed, it asks the shell to open the child's frame (or the parent's Children panel).</summary>
internal sealed partial class ChildAnswerButton : UserControl
{
    public ChildAnswerButton()
    {
        InitializeComponent();
    }

    private void OnFollow(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ChildAnswerViewModel answer)
        {
            (TopLevel.GetTopLevel(this)?.DataContext as ShellViewModel)?.FollowChildAnswer(answer);
        }
    }
}
