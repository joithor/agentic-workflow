#pragma warning disable CA1822, MA0041, IDE0060 // red stub: replaced by the green commit
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

    /// <summary>The name screen readers hear: the subject and the address.</summary>
    public string AccessibleName => $"{Subject} {_row.Address}";

    /// <summary>Where Enter and a click go: the instance in the Workflows frame.</summary>
    public DeepLink Link => new(DeepLinkKind.Workflow, _row.Address);

    /// <summary>The lock button for a protected answer; null for none.</summary>
    public ProtectedActionViewModel? Protected => null;

    /// <summary>True for the row the keys act on.</summary>
    public bool IsSelected { get; internal set => Set(ref field, value); }

    /// <summary>True while the views are stalled or rebuilding: the row stays, marked stale.</summary>
    public bool IsStale { get; private set => Set(ref field, value); }

    /// <summary>Everything the row draws: two rows with the same signature look the same.</summary>
    internal string Signature => string.Join('|', Address, Subject, Chip, Now, string.Join(',', Inputs.Select(i => i.Name)), string.Join(',', Stages.Select(s => s.Name)));

    /// <summary>Sets what the list knows of the row.</summary>
    /// <param name="stale">The views are stalled or rebuilding.</param>
    internal void SetStale(bool stale) => IsStale = stale;
}
