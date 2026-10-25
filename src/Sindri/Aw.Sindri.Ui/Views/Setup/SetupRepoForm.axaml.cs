using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Setup;

namespace Aw.Sindri.Ui.Views.Setup;

#pragma warning disable MA0182 // why: the step row places it in XAML, which the analyzer can't see
/// <summary>The repo step's form: a click or Enter only calls the view model; the view model asks where focus goes.</summary>
internal sealed partial class SetupRepoForm : UserControl
{
    private SetupRepoFormViewModel? _form;

    public SetupRepoForm()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _form?.FocusRequested -= OnFocusRequested;
        _form = DataContext as SetupRepoFormViewModel;
        _form?.FocusRequested += OnFocusRequested;
    }

    private void OnFocusRequested(object? sender, SetupFocusEventArgs e) => Dispatcher.UIThread.Post(() => SetupRepoPath.Focus(), DispatcherPriority.Input);

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        if (_form is not null)
        {
            await _form.Browse(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnAdd(object? sender, RoutedEventArgs e)
    {
        if (_form is not null)
        {
            await _form.Add(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnPathKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _form is { CanAdd: true })
        {
            e.Handled = true;
            await _form.Add(CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155
}
#pragma warning restore MA0182
