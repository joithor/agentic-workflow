namespace Aw.BugFix.Features.AdvanceInvestigate;

/// <summary>The advance investigate: snapshot the /rootCause handoff.</summary>
/// <param name="StateDirectory">The state directory.</param>
/// <param name="Handoff">The handoff path.</param>
public sealed record AdvanceInvestigateRequest(
    string StateDirectory,
    string Handoff);
