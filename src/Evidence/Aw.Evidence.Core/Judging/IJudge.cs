namespace Aw.Evidence.Core.Judging;

/// <summary>
/// The model-backed judge. Every method returns null when the judge is unavailable, times out or answers out of
/// contract; callers fail closed (a null repair is no repair, a null critique leaves the visual unchecked).
/// </summary>
public interface IJudge
{
    /// <summary>Proposes a visible element to replace a broken selector.</summary>
    /// <param name="request">The broken selector and candidates.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The proposal, or null.</returns>
    public ValueTask<SelectorRepair?> RepairSelectorAsync(SelectorRepairRequest request, CancellationToken cancellationToken);

    /// <summary>Judges a screenshot.</summary>
    /// <param name="request">The screenshots.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The verdict, or null.</returns>
    public ValueTask<VisualVerdict?> CritiqueAsync(VisualCritiqueRequest request, CancellationToken cancellationToken);
}
