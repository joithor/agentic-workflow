using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// The session sheet (collector spec CU-3): one session's fields, host, history and Focus. T0's skeleton: the seams exist
/// (<see cref="Open"/>, <see cref="Refresh"/>, <see cref="Preview"/>); the fields are T3's, in this file.
/// </summary>
/// <param name="api">The API.</param>
internal sealed class SessionSheetViewModel(ISindriApi api) : ObservableObject
{
    /// <summary>The API the sheet reads.</summary>
    internal ISindriApi Api { get; } = api;

    /// <summary>
    /// The live preview's seam (CO-7c, CU-8; the preview lives in this sheet only): null until CO-7c types and fills it. The view's
    /// LivePreviewHost stays hidden and empty while it is null.
    /// </summary>
    public object? Preview { get; private set => Set(ref field, value); }

    /// <summary>Reads the session behind a ref and shows it.</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="reference">The session's ref, provider/id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Open(Uri url, string reference, CancellationToken cancellationToken) => throw new NotSupportedException("T3");

    /// <summary>Reads the open session again (on every event while the sheet is open).</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken) => throw new NotSupportedException("T3");
}
