using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>
/// The app's local preferences over the head's store (UI spec §12.3): read clamped, reset to the defaults when there are
/// none or they don't parse, and written whole. The shell (window, sheet, last page) and Settings (text scale, reduce
/// motion) read and write through it.
/// </summary>
/// <param name="store">Where the head keeps them.</param>
[SuppressMessage("Design", "MA0182:Internal type is apparently never used", Justification = "U0 contract: Unit A's shell and Unit D's Settings read and write through it; until then only its tests use it.")]
internal sealed class Preferences(IPreferencesStore store)
{
    /// <summary>Reads the preferences.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The clamped preferences, or the defaults.</returns>
    internal async ValueTask<UiPreferences> Load(CancellationToken cancellationToken) =>
        await store.Read(cancellationToken).ConfigureAwait(false) is { } bytes ? PreferencesJson.Read(bytes) : UiPreferences.Defaults;

    /// <summary>Replaces the preferences.</summary>
    /// <param name="preferences">The preferences (clamped before they are written).</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when they are written.</returns>
    internal ValueTask Save(UiPreferences preferences, CancellationToken cancellationToken) => store.Write(PreferencesJson.Write(preferences), cancellationToken);
}
