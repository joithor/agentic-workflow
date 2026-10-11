using Aw.BugFix.Core.Model;

namespace Aw.BugFix.Features.AdvanceInvestigate;

/// <summary>The result of advance investigate.</summary>
/// <param name="Phase">The new phase.</param>
/// <param name="Hypotheses">How many hypotheses the handoff has.</param>
public sealed record AdvanceInvestigateResponse(
    Phase Phase,
    int Hypotheses);
