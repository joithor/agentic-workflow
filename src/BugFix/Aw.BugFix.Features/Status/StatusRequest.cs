namespace Aw.BugFix.Features.Status;

/// <summary>The status: the whole state.</summary>
/// <param name="StateDirectory">The state directory.</param>
public sealed record StatusRequest(
    string StateDirectory);
