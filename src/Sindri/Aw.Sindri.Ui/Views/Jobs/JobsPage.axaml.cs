using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Jobs;

namespace Aw.Sindri.Ui.Views.Jobs;

/// <summary>The Jobs page: a title click on a row opens the job's sheet.</summary>
internal sealed partial class JobsPage : UserControl
{
    public JobsPage()
    {
        InitializeComponent();
        AddHandler(JobRow.OpenEvent, OnOpen);
    }

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (e.Source is JobRow { Job: { } job } && DataContext is JobsPageViewModel page)
        {
            page.RequestOpen(job.Link);
        }
    }
}
