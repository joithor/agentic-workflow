namespace Aw.Judge.Host.Commands;

/// <summary>A provider the chain passed over, as the decision row prints it: {"provider", "reason"}.</summary>
/// <param name="Provider">The provider name.</param>
/// <param name="Reason">unavailable, failed, timeout or below_threshold.</param>
internal sealed record SkippedOutput(string Provider, string Reason);
