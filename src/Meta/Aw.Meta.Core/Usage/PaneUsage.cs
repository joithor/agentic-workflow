namespace Aw.Meta.Core.Usage;

/// <summary>What a pane's screen says about usage limits.</summary>
/// <param name="Stop">The last usage-limit message on the screen; null when there is none.</param>
/// <param name="Current">Whether it is the last output (nothing the lane did came after it).</param>
/// <param name="Idle">Whether the lane sits at an empty prompt with no work running, so a line can be typed.</param>
internal sealed record PaneUsage(UsageStop? Stop, bool Current, bool Idle);
