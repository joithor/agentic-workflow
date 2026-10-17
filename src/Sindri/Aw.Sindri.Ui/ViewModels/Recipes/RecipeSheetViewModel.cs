using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>The recipe sheet (screens spec §4.3, U10a): one recipe read by id, and by a hash when the link names one. Read-only.</summary>
internal sealed class RecipeSheetViewModel(ISindriApi api) : ObservableObject
{
    private Uri? _url;
    private string _subject = string.Empty;

    /// <summary>Where the read stands.</summary>
    public FramePhase Phase
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(IsProblem));
            OnChanged(nameof(InitialFocus));
        }
    }

    /// <summary>True while a refusal or an unreadable answer is shown.</summary>
    public bool IsProblem => Phase is FramePhase.NotFound or FramePhase.Unreadable or FramePhase.Refused;

    /// <summary>True when the recipe serves a name template.</summary>
    public bool HasName => Name.Length > 0;

    /// <summary>True when there is a note.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>Back's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string BackText => Labels.Back;

    /// <summary>Where focus lands: Back when there is nothing else to act on.</summary>
    public FrameFocus InitialFocus => FrameFocus.Back;
#pragma warning restore CA1822, MA0041

    /// <summary>The recipe's id.</summary>
    public string Id { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The header's identity line.</summary>
    public string Identity { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The name template, or empty.</summary>
    public string Name { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The sentence a refusal or an unreadable answer shows.</summary>
    public string ProblemText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The note under the header; empty when none.</summary>
    public string Note { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Opens a recipe and reads it.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="subject">The link's subject: an id, or an id, '@' and a hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the sheet shows the answer.</returns>
    internal async Task Open(Uri url, string subject, CancellationToken cancellationToken)
    {
        (_url, _subject) = (url, subject);
        Phase = FramePhase.Loading;
        var at = subject.IndexOf('@', StringComparison.Ordinal);
        _ = await api.Recipe(url, at < 0 ? subject : subject[..at], at < 0 ? null : subject[(at + 1)..], cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Re-reads the recipe shown.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the sheet shows the answer.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken) => _subject.Length == 0 ? Task.CompletedTask : Open(_url ?? url, _subject, cancellationToken);
}
