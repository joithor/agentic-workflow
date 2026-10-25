namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>What the decision stub decided for one report.</summary>
/// <param name="To">The state the matched transition enters.</param>
/// <param name="Park">The park reason ('rounds', 'no-progress', 'needs-answer') instead of entering, if any.</param>
/// <param name="Loop">The loop whose round the entry counts, if any.</param>
/// <param name="Round">That loop's round after the entry.</param>
/// <param name="Findings">The findings to compare the next round with.</param>
internal sealed record SpikeVerdict(string To, string? Park, string? Loop, int Round, string? Findings);
