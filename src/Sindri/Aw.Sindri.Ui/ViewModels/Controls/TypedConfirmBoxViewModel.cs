using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Controls;

#pragma warning disable MA0182, CA1822, MA0041 // red stub, used by the tests only until the green commit

/// <summary>The typed confirmation's state (setup spec §2.1). Red stub: the match rules arrive with the green commit.</summary>
internal sealed class TypedConfirmBoxViewModel : ObservableObject
{
    internal TypedConfirmBoxViewModel(string action, CatalogViewModel catalog, TimeProvider clock)
    {
        throw new NotSupportedException();
    }

    /// <summary>What the person has typed.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>True when the button can be pressed.</summary>
    public bool CanSubmit => throw new NotSupportedException();

    /// <summary>Takes the served challenge: its text is the phrase.</summary>
    /// <param name="challenge">The challenge.</param>
    internal void Offer(AppChallengeView challenge) => throw new NotSupportedException();
}
