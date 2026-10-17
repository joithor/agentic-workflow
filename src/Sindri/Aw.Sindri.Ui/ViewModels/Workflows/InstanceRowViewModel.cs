using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// One row of the Workflows list (screens §4.2): the subject, the inputs, a cell per state, and the served now and chip. It decides
/// nothing: every word is the row's own.
/// </summary>
internal sealed class InstanceRowViewModel : ObservableObject
{
    private const string CancelCommand = "ApplyCommand:cancel";

    private readonly InstanceSummary _row;

    internal InstanceRowViewModel(InstanceSummary row, IClipboard clipboard, TimeProvider clock)
    {
        _row = row;
        var blocked = row.Inputs.Any(i => i is { Required: true, Present: false });
        Inputs = [.. row.Inputs.Select(i => new InputCellViewModel(i))];
        Stages = [.. row.States.Select(s =>
        {
            var current = string.Equals(s.State, row.State, StringComparison.Ordinal);
            return new StageCellViewModel(s, current, blocked && current);
        })];
        if (InstanceRules.IsChildPointer(row.Answer))
        {
            ChildAnswer = ChildAnswerViewModel.ForInstance(row.Address, row.Answer!.Child); // why: IsChildPointer is true only with an answer
        }
        else if (InstanceRules.HasProtectedAnswer(row))
        {
            var answer = row.Answer!; // why: HasProtectedAnswer is true only for a row with an answer
            Protected = new ProtectedActionViewModel(new ProtectedAction(Labels.AnswerVerb(answer.Kind), answer.Cli, row.Address), clipboard, clock);
        }

        Cells = Stages;
        CanAnswerAtDesk = row.Answer is null && InstanceRules.CompletesAtDesk(row);
        CanCancel = row.Actions.Contains(CancelCommand, StringComparer.Ordinal);
    }

    /// <summary>The instance's address, as served.</summary>
    public string Address => _row.Address;

    /// <summary>The instance's own id, as served.</summary>
    public string InstanceId => _row.Id;

    /// <summary>The state the instance is in, as served.</summary>
    public string StateWord => _row.State;

    /// <summary>The compact line a narrow page shows instead of the columns: the id and the state.</summary>
    public string Compact => $"{_row.Address} · {_row.State}";

    /// <summary>The recipe's id, as served.</summary>
    public string Recipe => _row.Recipe.Id;

    /// <summary>The subject: its served label, or its key when it has none.</summary>
    public string Subject => _row.Subject.Label.Length > 0 ? _row.Subject.Label : _row.Subject.Key;

    /// <summary>The served chip.</summary>
    public StateChip Chip => _row.Chip;

    /// <summary>The served now sentence, unchanged.</summary>
    public string Now => _row.Now;

    /// <summary>A cell per declared input.</summary>
    public IReadOnlyList<InputCellViewModel> Inputs { get; }

    /// <summary>A cell per state, in the served order.</summary>
    public IReadOnlyList<StageCellViewModel> Stages { get; }

    /// <summary>The cells under the group's column heads, one per column in the group's order (a blank where this row has no such state).</summary>
    public IReadOnlyList<StageCellViewModel> Cells { get; private set => Set(ref field, value); }

    /// <summary>True when the row declares inputs; with none the inputs column has nothing to name and stays out of the control view.</summary>
    public bool HasInputs => Inputs.Count > 0;

    /// <summary>What a screen reader hears for the inputs column: the cells' names in one.</summary>
    public string InputsName => string.Join(", ", Inputs.Select(i => i.Name));

    /// <summary>What a screen reader hears for the stages column: the cells' names in one.</summary>
    public string StagesName => string.Join(", ", Stages.Select(c => c.Name));

    /// <summary>Lines the cells up under the group's columns.</summary>
    /// <param name="columns">The group's columns.</param>
    internal void Align(IReadOnlyList<string> columns) =>
        Cells = [.. columns.Select(column => Stages.FirstOrDefault(s => string.Equals(s.State, column, StringComparison.Ordinal)) ?? StageCellViewModel.Missing(column))];

    /// <summary>The title button's accessible name.</summary>
    public string OpenName => Labels.OpenInstanceName(Subject, _row.Address);

    /// <summary>The name screen readers hear: the subject and the address, then the state and the chip's word.</summary>
    public string AccessibleName => $"{Subject} {_row.Address}, {_row.State}, {_row.Chip.Word}";

    /// <summary>Where Enter and a click go: the instance in the Workflows frame.</summary>
    public DeepLink Link => new(DeepLinkKind.Workflow, _row.Address);

    /// <summary>The lock button for a protected answer; null for none.</summary>
    public ProtectedActionViewModel? Protected { get; }

    /// <summary>The "Answer in the child" control of a row whose answer a child decides; null for any other row.</summary>
    public ChildAnswerViewModel? ChildAnswer { get; }

    /// <summary>True when A answers at the desk: the row's current state is a desk state the engine lets the human complete.</summary>
    public bool CanAnswerAtDesk { get; }

    /// <summary>True when the engine lists Cancel for the row (X asks).</summary>
    public bool CanCancel { get; }

    /// <summary>True for the row the keys act on.</summary>
    public bool IsSelected { get; internal set => Set(ref field, value); }

    /// <summary>True while the views are stalled or rebuilding: the row stays, marked stale.</summary>
    public bool IsStale { get; private set => Set(ref field, value); }

    /// <summary>Everything the row draws: two rows with the same signature look the same.</summary>
    internal string Signature => string.Join('|', Address, Subject, Chip, Now, string.Join(',', Inputs.Select(i => i.Name)), string.Join(',', Stages.Select(s => s.Name)), StateWord, Chip.Word, Protected?.Cli, ChildAnswer?.Target, ChildAnswer?.Intent, CanAnswerAtDesk, CanCancel);

    /// <summary>Sets what the list knows of the row.</summary>
    /// <param name="stale">The views are stalled or rebuilding.</param>
    internal void SetStale(bool stale) => IsStale = stale;
}
