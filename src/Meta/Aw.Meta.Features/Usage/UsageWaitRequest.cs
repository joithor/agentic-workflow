namespace Aw.Meta.Features.Usage;

/// <summary>The UsageWait request: block until the window is back.</summary>
/// <param name="UntilOk">Whether to keep waiting until nothing reads as limited, not only until every reset time has passed.</param>
/// <param name="Max">The longest to wait.</param>
public sealed record UsageWaitRequest(bool UntilOk, TimeSpan Max);
