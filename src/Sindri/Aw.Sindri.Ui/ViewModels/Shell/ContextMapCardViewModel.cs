using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The Health page's Context map card (code index spec 6.4, F1P-c): the map's publish state in one word with its glyph, then Sindri's sentences
/// (which repos wait, or the stale commits). It reads GET /sindri/context/map/publish with the other followed views and shows nothing while no
/// map exists or the read is refused (a session-less app is the human, so a refusal is "not served yet", not a state).
/// </summary>
/// <param name="api">Its API.</param>
internal sealed class ContextMapCardViewModel(ISindriApi api) : ObservableObject
{
    /// <summary>True once a read answered: the card shows nothing before that.</summary>
    public bool IsVisible { get; private set => Set(ref field, value); }

    /// <summary>The state in words ("Held for the code index").</summary>
    public string State { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The state's glyph, drawn before the word (§11.3): a word always comes with it, never colour alone.</summary>
    public StateGlyph Glyph { get; private set => Set(ref field, value); }

    /// <summary>Sindri's sentences for the state, at most two repos and then "and N more".</summary>
    public IReadOnlyList<string> Lines { get; private set => Set(ref field, value); } = [];

    /// <summary>The card as one accessible name: the state, then the sentences.</summary>
    public string Name { get; private set => Set(ref field, value); } = Labels.ContextMapCard;

    /// <summary>Reads the publish state and shows it; a refusal or a failed read hides the card.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        var read = await api.MapPublish(url, cancellationToken).ConfigureAwait(true);
        if (read.IsError)
        {
            Hide();
            return;
        }

        Show(read.Value);
    }

    private void Show(MapPublishView view)
    {
        var word = Labels.MapPublishWord(view.State, view.Word);
        State = word;
        Glyph = GlyphOf(view.State);
        Lines = view.Lines;
        Name = Labels.ContextMapCard + ": " + word + (view.Lines.Count == 0 ? string.Empty : ". " + string.Join(' ', view.Lines));
        IsVisible = true;
    }

    private void Hide()
    {
        IsVisible = false;
        State = string.Empty;
        Glyph = StateGlyph.None;
        Lines = [];
        Name = Labels.ContextMapCard;
    }

    // A map that is current and matches is ok; one held for the index waits (⌛); one that needs a look (overdue, answers from another commit) is ◇.
    private static StateGlyph GlyphOf(string state) => state switch
    {
        "held" => StateGlyph.Stale,
        "overdue" or "published-stale" => StateGlyph.Profile,
        _ => StateGlyph.Ok,
    };
}
