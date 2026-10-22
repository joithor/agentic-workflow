namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The Needs-you inbox page (<see cref="NeedsYouPageViewModel"/>, continued): the card footer, with the row's timeout (slice 5).</summary>
internal sealed partial class NeedsYouPageViewModel
{
    /// <summary>The card's footer, right: how long it has waited; empty when the row serves no time.</summary>
    public string FooterRight => Card?.Entry is { Since: var since } entry && since != default ? Waiting(entry, since) : "";

    // The wait, then what the row's timeout does and how long is left of it (never below zero).
    private string Waiting(NeedsYouEntry entry, DateTimeOffset since)
    {
        var now = _clock.GetUtcNow();
        var waited = Labels.NeedsYouWaited(now - since);
        return entry.Timeout is { } timeout
            ? Labels.NeedsYouFootTimeout(waited, timeout.Kind, Labels.NeedsYouTimeLeft(timeout.Due - now))
            : Labels.NeedsYouFootRight(waited);
    }
}
