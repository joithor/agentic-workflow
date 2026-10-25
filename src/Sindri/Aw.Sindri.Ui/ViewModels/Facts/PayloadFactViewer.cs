using System.Text.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>
/// A payload fact, and every type with no viewer of its own: View reads the fact's route and shows the body once, as a scrubbed tree (a lone value as one line). A body the engine cut short says so; a fact the engine doesn't have shows the sentence it served.
/// </summary>
internal sealed class PayloadFactViewer : ObservableObject, IFactViewer
{
    private readonly FactViewerContext _context;

    internal PayloadFactViewer(FactRow row, FactViewerContext context)
    {
        (Row, _context) = (row, context);
    }

    /// <summary>The fact row the viewer shows.</summary>
    public FactRow Row { get; }

    /// <summary>The polite line that says the outcome of the read: the problem, the cut, or that the body is there.</summary>
    public Components.LiveAnnouncer Voice { get; } = new(Labels.ReadingFact);

    /// <summary>True until the body has been read.</summary>
    public bool IsLoading { get; private set { Set(ref field, value); OnChanged(nameof(IsShown)); } } = true;

    /// <summary>True once there is a body to show.</summary>
    public bool IsShown => !IsLoading && ProblemText.Length == 0;

    /// <summary>The words while the body is read.</summary>
#pragma warning disable CA1822, MA0041 // a fixed sentence is a property because the view binds it
    public string LoadingText => Labels.ReadingFact;
#pragma warning restore CA1822, MA0041

    /// <summary>The served sentence when the body can't be shown; empty otherwise.</summary>
    public string ProblemText { get; private set { Set(ref field, value); OnChanged(nameof(HasProblem)); OnChanged(nameof(IsShown)); } } = string.Empty;

    /// <summary>True when the body can't be shown.</summary>
    public bool HasProblem => ProblemText.Length > 0;

    /// <summary>A body that is not an object or an array, as one line; empty when the tree draws the body. The body is drawn once.</summary>
    public string ScalarLine { get; private set { Set(ref field, value); OnChanged(nameof(HasScalarLine)); } } = string.Empty;

    /// <summary>True when the body is a lone value the tree can't draw.</summary>
    public bool HasScalarLine => ScalarLine.Length > 0;

    /// <summary>The whole body as a tree.</summary>
    public IReadOnlyList<PayloadNodeViewModel> Tree { get; private set => Set(ref field, value); } = [];

    /// <summary>The engine's truncation, in words; empty when the body is whole.</summary>
    public string TruncatedNote { get; private set { Set(ref field, value); OnChanged(nameof(HasTruncatedNote)); } } = string.Empty;

    /// <summary>True when the body was cut short.</summary>
    public bool HasTruncatedNote => TruncatedNote.Length > 0;

    /// <summary>Reads the fact's route.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the viewer shows the answer.</returns>
    public async Task Load(CancellationToken cancellationToken)
    {
        if (_context.ApiUrl() is not { } url)
        {
            ProblemText = Labels.PayloadNotRunning;   // not an empty body that reads as loaded
            await Task.Yield();   // the live region is created with the viewer: its first words are said a turn later, so the change is spoken
            Voice.Say(ProblemText);
            IsLoading = false;
            return;
        }

        var answer = await _context.Api.InstanceFact(url, _context.InstanceId, Row.Key, cancellationToken).ConfigureAwait(true);
        if (answer.IsError)
        {
            ProblemText = string.Equals(answer.Error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal) ? Labels.StripUnreadable : answer.Error.Message;
            Voice.Say(ProblemText);
        }
        else
        {
            Show(answer.Value);
            Voice.Say(TruncatedNote.Length > 0 ? TruncatedNote : Labels.PayloadLoaded(Row.Label));
        }

        IsLoading = false;
    }

    private void Show(FactView fact)
    {
        var tree = PayloadNodeViewModel.Roots(fact.Body);
        Tree = tree;
        ScalarLine = fact.Body.ValueKind is JsonValueKind.Object or JsonValueKind.Array ? string.Empty : PayloadNodeViewModel.Line(fact.Body);
        TruncatedNote = fact.Truncated ? Labels.PayloadTruncated : string.Empty;
    }
}
