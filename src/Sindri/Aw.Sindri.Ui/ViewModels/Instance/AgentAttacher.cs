using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// Sends Attach for one item: OpenItem:attach with the version the app read (UI spec E-2), then reads the launch it started
/// for the viewer's outcome. The server opens the viewer; the app execs nothing. Sent once, never retried.
/// </summary>
/// <param name="api">The API.</param>
/// <param name="url">The base URL now, or null when there is no API.</param>
/// <param name="itemId">The item.</param>
internal sealed class AgentAttacher(ISindriApi api, Func<Uri?> url, string itemId)
{
    /// <summary>Attaches to the item's live session.</summary>
    /// <param name="itemVersion">The item's version as the app read it.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>The outcome as a sentence.</returns>
    internal async Task<AttachOutcome> Attach(long itemVersion, CancellationToken cancellationToken)
    {
        if (url() is not { } baseUrl)
        {
            return Refusal(SindriUiErrors.NotRunning.With("no API to send to"));
        }

        var opened = await api.OpenItem(baseUrl, itemId, new OpenItemRequest("attach", itemVersion), cancellationToken).ConfigureAwait(true);
        if (opened.IsError)
        {
            return Refusal(opened.Error);
        }

        var launch = await api.Launch(baseUrl, opened.Value.Launch, cancellationToken).ConfigureAwait(true);
        if (launch.IsError)
        {
            return new AttachOutcome(Labels.LaunchUnreadable, string.Empty, Refused: false);   // accepted, so not a refusal: the run line shows the launch when it can be read
        }

        var view = launch.Value;
        var command = RunWords.ShowsCommand(view) ? RunWords.Command(view) : string.Empty;
        var line = command.Length > 0 ? Labels.AttachRunThis : RunWords.LaunchLine(view);
        return new AttachOutcome(line, command, string.Equals(view.State, "failed", StringComparison.Ordinal));
    }

    // A code the app has words for gets them; any other is shown as served (its message carries the fix).
    private static AttachOutcome Refusal(Error error) =>
        new(Labels.AttachRefusal(error.Code) ?? $"{error.Message} ({error.Code})", string.Empty, Refused: true);
}
