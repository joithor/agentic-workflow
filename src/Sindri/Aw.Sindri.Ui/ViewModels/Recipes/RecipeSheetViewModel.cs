using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>The recipe sheet (screens spec §4.3, U10a): one recipe read by id, and by a hash when the link names one. Read-only.</summary>
internal sealed class RecipeSheetViewModel(ISindriApi api) : ObservableObject
{
    private Uri? _url;
    private string _subject = string.Empty;
    private int _generation;

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
    public string Name { get; private set { Set(ref field, value); OnChanged(nameof(HasName)); } } = string.Empty;

    /// <summary>The sentence a refusal or an unreadable answer shows.</summary>
    public string ProblemText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The note under the header; empty when none.</summary>
    public string Note { get; private set { Set(ref field, value); OnChanged(nameof(HasNote)); } } = string.Empty;

    /// <summary>Opens a recipe and reads it.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="subject">The link's subject: an id, or an id, '@' and a hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the sheet shows the answer.</returns>
    internal Task Open(Uri url, string subject, CancellationToken cancellationToken)
    {
        if (!string.Equals(subject, _subject, StringComparison.Ordinal))
        {
            Clear();
            _subject = subject;
            Id = Split(subject).Recipe;
            Phase = FramePhase.Loading;
        }

        _url = url;
        return Read(cancellationToken);
    }

    /// <summary>Re-reads the recipe shown.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the sheet shows the answer.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return _subject.Length == 0 ? Task.CompletedTask : Read(cancellationToken);
    }

    private static (string Recipe, string? Hash) Split(string subject)
    {
        var at = subject.IndexOf('@', StringComparison.Ordinal);
        return at < 0 ? (subject, null) : (subject[..at], subject[(at + 1)..]);
    }

    private async Task Read(CancellationToken cancellationToken)
    {
        if (_url is not { } url)
        {
            return;
        }

        var (recipe, hash) = Split(_subject);
        var generation = ++_generation;
        var answer = await api.Recipe(url, recipe, hash, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;   // a newer read, or another recipe, took over
        }

        if (answer.IsSuccess)
        {
            Show(answer.Value);
        }
        else
        {
            Failed(answer.Error);
        }
    }

    private void Show(RecipeView recipe)
    {
        Id = recipe.Id;
        Identity = string.Create(CultureInfo.InvariantCulture, $"{recipe.Id}@{Labels.Hash12(recipe.Hash)} · v{recipe.Version} · {Labels.UrgencyWord(recipe.Urgency)}");
        Name = recipe.Name ?? string.Empty;
        Note = string.Empty;
        ProblemText = string.Empty;
        Phase = FramePhase.Shown;
    }

    private void Failed(Error error)
    {
        if (Phase == FramePhase.Shown)
        {
            Note = error.Kind == ErrorKind.NotFound ? Labels.RecipeGone : Note;   // what was shown stays
        }
        else if (error.Kind == ErrorKind.NotFound)
        {
            Problem(FramePhase.NotFound, error.Message);
        }
        else if (string.Equals(error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal))
        {
            Problem(FramePhase.Unreadable, Labels.StripUnreadable);
        }
        else
        {
            Problem(FramePhase.Refused, Labels.InstanceRefused(error));
        }
    }

    private void Problem(FramePhase phase, string text)
    {
        ProblemText = text;
        Phase = phase;
    }

    private void Clear()
    {
        _generation++;
        (Id, Identity, Name, Note, ProblemText) = (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    }
}
