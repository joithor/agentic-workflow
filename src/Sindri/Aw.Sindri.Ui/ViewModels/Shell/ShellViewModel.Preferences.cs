using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The shell's local preferences (§3.2, §12.3): the window's bounds, the sheet's width and the last page.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Remembers the sheet's width (clamped to 420–760 when it's next read).</summary>
    /// <param name="width">The width the user dragged it to.</param>
    internal void ResizeSheet(double width)
    {
        Remember(p => p with { SheetWidth = width });
        OnChanged(nameof(SheetWidth));
    }

    /// <summary>Remembers the window's bounds (grown to 900 × 600 when they're next read).</summary>
    /// <param name="bounds">Where the window is.</param>
    internal void RememberWindow(WindowBounds bounds) => Remember(p => p with { Window = bounds });

    /// <summary>Reads the stored preferences: the last page, the window's bounds and the sheet's width (the defaults when they can't be read).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when they are applied.</returns>
    internal async Task Restore(CancellationToken cancellationToken)
    {
        try
        {
            _preferences = await _store.Load(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _preferences = UiPreferences.Defaults;
        }

        CurrentPage = Enum.TryParse<PageId>(_preferences.LastPage, out var page) ? page : PageId.Home;
        Changed(_pageProperties);
        Changed(nameof(Window), nameof(SheetWidth));
    }

    // The shell's fields are written over whatever the file holds now (Settings writes its own), whole and in order (each
    // write waits for the one before); a failed write keeps the old file, since they are a convenience and never change
    // what the API decides.
    private void Remember(Func<UiPreferences, UiPreferences> change)
    {
        _preferences = change(_preferences).Clamp();
        _saving = SaveAfter(_saving, change);
    }

    private async Task SaveAfter(Task previous, Func<UiPreferences, UiPreferences> change)
    {
        await previous.ConfigureAwait(true);
        try
        {
            var stored = await _store.Load(CancellationToken.None).ConfigureAwait(true);
            await _store.Save(change(stored), CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Kept in memory for this run; the next change tries again.
        }
    }
}
