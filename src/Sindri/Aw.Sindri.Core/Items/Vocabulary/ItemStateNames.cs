
namespace Aw.Sindri.Core.Items.Vocabulary;

/// <summary>The wire names of the item states, for the text a person reads (the JSON converter writes the same names).</summary>
internal static class ItemStateNames
{
    /// <summary>The kebab-case name of <paramref name="state"/>.</summary>
    /// <param name="state">The state.</param>
    /// <returns>For example 'needs-decision'.</returns>
    public static string Of(ItemState state) => state switch
    {
        ItemState.New => "new",
        ItemState.Queued => "queued",
        ItemState.Running => "running",
        ItemState.NeedsDecision => "needs-decision",
        ItemState.NeedsAnswer => "needs-answer",
        ItemState.Parked => "parked",
        ItemState.TakenOver => "taken-over",
        ItemState.Done => "done",
        ItemState.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "not an item state"),
    };
}
