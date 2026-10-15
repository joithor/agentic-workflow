using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>
/// The app's local preferences over the head's store (UI spec §12.3): read clamped, reset to the defaults when there are
/// none or they don't parse, and written whole. The app holds one, which the shell (window, sheet, last page) and Settings
/// (text scale, reduce motion) share: every write goes through its one queue, each reading the file only after the write
/// before it, so a writer never writes back what it read before another's change.
/// </summary>
/// <param name="store">Where the head keeps them.</param>
internal sealed class Preferences(IPreferencesStore store)
{
    private readonly Lock _gate = new();
    private Task _last = Task.CompletedTask;

    /// <summary>Reads the preferences.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The clamped preferences, or the defaults.</returns>
    internal async ValueTask<UiPreferences> Load(CancellationToken cancellationToken) =>
        await store.Read(cancellationToken).ConfigureAwait(false) is { } bytes ? PreferencesJson.Read(bytes) : UiPreferences.Defaults;

    /// <summary>
    /// Changes the stored preferences after every write queued before: reads the file, applies <paramref name="change"/>
    /// and writes it whole (clamped). A read or write the platform refuses leaves the file as it was; the next change tries
    /// again, since preferences are a convenience and never change what the API decides.
    /// </summary>
    /// <param name="change">The change, applied to what the file holds by then.</param>
    /// <returns>A task that completes when the change is written or refused (it never faults).</returns>
    internal async Task Update(Func<UiPreferences, UiPreferences> change)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task before;
        lock (_gate)
        {
            before = _last;
            _last = done.Task;
        }

        try
        {
            await before.ConfigureAwait(false);
            var stored = await Load(CancellationToken.None).ConfigureAwait(false);
            await store.Write(PreferencesJson.Write(change(stored)), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The file keeps its old contents; the change stays in memory for this run.
        }
        finally
        {
            done.SetResult();
        }
    }

    /// <summary>The writes queued so far.</summary>
    /// <returns>A task that completes when every one is written or refused.</returns>
    internal Task Flush()
    {
        lock (_gate)
        {
            return _last;
        }
    }
}
