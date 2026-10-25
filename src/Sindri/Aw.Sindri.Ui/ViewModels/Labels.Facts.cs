using System.Globalization;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the facts panel and its viewers (screens §4.2, §5.2). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The panel's heading.</summary>
    internal const string FactsHeading = "What this run collected";

    /// <summary>The words of a recipe that declares no facts.</summary>
    internal const string NoFacts = "This workflow collects nothing.";

    /// <summary>Why the panel is there, under its heading.</summary>
    internal const string FactsHint = "Each stage adds what it found; later stages and agents read it.";

    /// <summary>What a human actor reads as.</summary>
    internal const string ByYou = "you";

    /// <summary>The View button, closed.</summary>
    internal const string ViewFact = "Show";

    /// <summary>The View button, open.</summary>
    internal const string HideFact = "Hide";

    /// <summary>The words of a body the engine cut short.</summary>
    internal const string PayloadTruncated = "Only the first part is shown; the rest was cut off.";

    /// <summary>The words while a body is read.</summary>
    internal const string ReadingFact = "Reading…";

    /// <summary>What the payload viewer says when there is no API to ask.</summary>
    internal const string PayloadNotRunning = "Sindri isn't running, so this can't be read now.";

    /// <summary>What the payload viewer's polite line says once the body is there.</summary>
    /// <param name="label">The fact's label.</param>
    /// <returns>The words.</returns>
    internal static string PayloadLoaded(string label) => $"{label} loaded";

    /// <summary>Open on a file or an item.</summary>
    internal const string OpenFact = "Open";

    /// <summary>The mark of a fact by how far it is.</summary>
    /// <param name="status">The fact's status.</param>
    /// <returns>● produced, ◐ in progress, ○ pending.</returns>
    internal static string FactMark(FactStatus status) => status switch
    {
        FactStatus.Produced => MarkDone,
        FactStatus.InProgress => MarkActive,
        FactStatus.Pending => MarkPending,
        _ => MarkPending,
    };

    /// <summary>The status in words (a glyph is never the only sign).</summary>
    /// <param name="status">The fact's status.</param>
    /// <returns>The words.</returns>
    internal static string FactStatusWords(FactStatus status) => status switch
    {
        FactStatus.Produced => "done",
        FactStatus.InProgress => "in progress",
        FactStatus.Pending => "waiting",
        _ => "waiting",
    };

    /// <summary>The state a fact came from.</summary>
    /// <param name="state">The state.</param>
    /// <returns>"from scoping".</returns>
    internal static string FromState(string state) => $"from {state}";

    /// <summary>A progress figure.</summary>
    /// <param name="progress">The served progress.</param>
    /// <returns>"4 of 9".</returns>
    internal static string ProgressOf(Progress progress) => string.Create(CultureInfo.InvariantCulture, $"{progress.Done} of {progress.Total}");

    /// <summary>The accessible name of a fact row's View button.</summary>
    /// <param name="open">True when the viewer is open.</param>
    /// <param name="label">The fact's label.</param>
    /// <param name="address">The instance.</param>
    /// <returns>The words.</returns>
    internal static string ViewFactName(bool open, string label, string address) => $"{(open ? HideFact : ViewFact)} {label} of {address}";

    /// <summary>The facts panel's accessible name.</summary>
    /// <param name="address">The instance.</param>
    /// <returns>The words.</returns>
    internal static string FactsName(string address) => $"What this run collected, {address}";

    /// <summary>A file's size in words.</summary>
    /// <param name="bytes">The size.</param>
    /// <returns>"900 B", "18 KB", "1.5 MB".</returns>
    internal static string FileSize(long bytes) => bytes switch
    {
        < 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes} B"),
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024d:0.#} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024d * 1024d):0.#} MB"),
    };

    /// <summary>Open on a file's accessible name.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The words.</returns>
    internal static string OpenFileName(string path) => $"Open {path}";

    /// <summary>What Open found when it could not open a file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The words.</returns>
    internal static string FileNotOpened(string path) => $"{path} can't be opened from here. Open it from the repository.";

    /// <summary>Open on an item's accessible name.</summary>
    /// <param name="itemId">The item.</param>
    /// <returns>The words.</returns>
    internal static string OpenItemName(string itemId) => $"Open item {itemId}";

    /// <summary>Open on a link's accessible name.</summary>
    /// <param name="title">The link's title.</param>
    /// <param name="host">The link's host.</param>
    /// <returns>The words.</returns>
    internal static string OpenLinkName(string title, string host) => $"Open {(title.Length > 0 ? title : "link")} in {host}";

    /// <summary>Copy on a text fact's accessible name.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The words.</returns>
    internal static string CopyLineName(string line) => $"{Copy}: {line}";
}
