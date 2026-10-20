using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Controls;

#pragma warning disable MA0182, CA1822, MA0041 // red stub, used by the tests only until the green commit

/// <summary>The hold-to-approve button's state (setup spec §2.2). Red stub: the lifecycle arrives with the green commit.</summary>
internal sealed class HoldButtonViewModel : ObservableObject
{
    internal HoldButtonViewModel(string action, CatalogViewModel catalog, TimeProvider clock, Func<bool>? reducedMotion = null)
    {
        throw new NotSupportedException();
    }

    /// <summary>True while the button is held.</summary>
    public bool IsHolding => throw new NotSupportedException();

    /// <summary>Starts a hold.</summary>
    /// <returns>True when the hold started.</returns>
    internal bool Begin() => throw new NotSupportedException();

    /// <summary>One step of the hold.</summary>
    /// <returns>The time held in milliseconds once the hold is complete; null before.</returns>
    internal int? Tick() => throw new NotSupportedException();

    /// <summary>Lets go before the end.</summary>
    internal void Cancel() => throw new NotSupportedException();

    /// <summary>Takes the served challenge's shortest hold.</summary>
    /// <param name="challenge">The challenge.</param>
    internal void Offer(AppChallengeView challenge) => throw new NotSupportedException();
}
