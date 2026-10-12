using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The status as labelled values (UI spec §4.2, §4.8): Home's health strip cells and the Health page's rows. Mapping
/// only; the words are <see cref="StatusText"/>'s.
/// </summary>
internal sealed class HealthDetail : ObservableObject
{
    /// <summary>Home's health strip: Process, Epoch · lease, API, Services, Profile.</summary>
    public IReadOnlyList<HealthCell> Strip { get; private set => Set(ref field, value); } = [];

    /// <summary>The Health page's process rows: pid · epoch, host lease, API.</summary>
    public IReadOnlyList<HealthCell> Process { get; private set => Set(ref field, value); } = [];

    /// <summary>The Health page's profile head: its glyph and state word.</summary>
    public string ProfileHead { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The Health page's profile rows: approved, live (when it differs), file.</summary>
    public IReadOnlyList<HealthCell> Profile { get; private set => Set(ref field, value); } = [];

    /// <summary>A version mismatch's two versions as labelled rows (states.html): this app's, then Sindri's.</summary>
    public IReadOnlyList<HealthCell> Versions { get; private set => Set(ref field, value); } = [];

    /// <summary>Shows a version mismatch.</summary>
    /// <param name="sindri">The API version Sindri reports.</param>
    internal void Mismatch(int sindri) => Versions = [new(nameof(sindri), sindri.ToString(System.Globalization.CultureInfo.InvariantCulture))];

    /// <summary>Shows a status.</summary>
    /// <param name="url">Where the API answered.</param>
    /// <param name="status">The status.</param>
    internal void Show(Uri url, StatusView status)
    {
        Strip = [.. StatusText.Strip(status)];
        Process = [.. StatusText.Process(url, status)];
        ProfileHead = StatusText.ProfileHead(status.Profile);
        Profile = [.. StatusText.Profile(status.Profile)];
    }
}
