using Avalonia.Controls;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Setup;

namespace Aw.Sindri.Ui.Views;

/// <summary>The Settings page: binds to the settings view model and holds no logic beyond bringing the setup checklist into view when it is asked for.</summary>
internal sealed partial class SettingsView : UserControl
{
    private SetupViewModel? _setup;

    public SettingsView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _setup?.RevealRequested -= OnRevealRequested;
        _setup = (DataContext as SettingsViewModel)?.Setup;
        _setup?.RevealRequested += OnRevealRequested;
    }

    // Opened by a link, G U, ⌘, or Home's line: the checklist is the first section, so it is brought to the top of the page.
    private void OnRevealRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => this.FindControl<Control>("SetupSection")?.BringIntoView(), DispatcherPriority.Loaded);
}
