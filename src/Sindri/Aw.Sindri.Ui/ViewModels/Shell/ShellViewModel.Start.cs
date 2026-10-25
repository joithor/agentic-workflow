using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Start;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Start form in the shell (<see cref="ShellViewModel"/>, continued; U5').</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Start form.</summary>
    public StartFormViewModel Start { get; }

    /// <summary>
    /// Opens the Start form: from `N` on Workflows, the Workflows head and Home's buttons, ⌘N and the palette. With an item's sheet open the
    /// form starts for that item.
    /// </summary>
    internal void OpenStart()
    {
        var item = IsSheetOpen && IsItemSheet && SheetLink?.Subject is { } subject ? subject : null;
        _ = Start.Open(item, CancellationToken.None);
    }

    // Everything the form needs once it exists (ShellViewModel's constructor calls this one line): an accepted start opens the instance's frame.
    private StartFormViewModel InitStart(ISindriApi api, WindowPorts window)
    {
        var form = new StartFormViewModel(Health.Catalog, Health.ApiUrl, api, window.Files);
        form.Started += (_, e) => OpenInstance(e.Address, e.Id);
        Workflows.StartRequested += (_, _) => OpenStart();
        return form;
    }
}
