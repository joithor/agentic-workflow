namespace Aw.BugFix.Features.Init;

/// <summary>The result of Init.</summary>
/// <param name="State">The state file's full path.</param>
public sealed record InitResponse(
    string State);
