#pragma warning disable MA0041, CA1822, CA1024, MA0182 // red stub
namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>Red stub: the decision sheet.</summary>
internal sealed class DecisionSheetViewModel : ObservableObject
{
    internal IReadOnlyList<DecisionOption> Options => throw new NotSupportedException();

    internal string Question => throw new NotSupportedException();

    internal string Note { get; set; } = "";

    internal string Refusal => throw new NotSupportedException();

    internal string Outcome => throw new NotSupportedException();

    internal bool IsCleared => throw new NotSupportedException();

    internal Task Settled => throw new NotSupportedException();

    internal Task Choose(string optionId) => throw new NotSupportedException();
}
