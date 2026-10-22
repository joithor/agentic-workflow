using System.Text.Json.Nodes;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// The design station's checks (line spec §4.3): one pure function over the facts, run by 'step.design.check' (draft) and 'step.design.evidence'. The
/// evidence stage runs every rule. DSN-XSPEC-001, DSN-XSPEC-002 and DSN-STYLE-001 read the repo at the scope packet's base sha and the style pack's pack.json,
/// which the block runtime has no reader for: a design that has contract edits gets a warning that they were not checked here, never a pass. DSN-STYLE-001 is checked when the caller passes the
/// pinned style pack; without one a design that has sections gets the same warning.
/// </summary>
internal static partial class DesignChecks
{
    /// <summary>Runs the rules of the stage.</summary>
    /// <param name="scope">The 'fact:scope' tree.</param>
    /// <param name="design">The 'fact:design' tree.</param>
    /// <param name="lensRounds">The 'fact:lensRounds' tree (evidence stage).</param>
    /// <param name="accepted">The 'fact:accepted' tree (evidence stage).</param>
    /// <param name="evidence">True for the evidence stage.</param>
    /// <returns>The report.</returns>
    internal static LineReport Run(JsonNode? scope, JsonNode? design, JsonNode? lensRounds, JsonNode? accepted, bool evidence) =>
        RunStyled(scope, design, (lensRounds, accepted), evidence, style: null);

    /// <summary>Runs the rules of the stage, DSN-STYLE-001 against the pinned style pack.</summary>
    /// <param name="scope">The 'fact:scope' tree.</param>
    /// <param name="design">The 'fact:design' tree.</param>
    /// <param name="lens">The 'fact:lensRounds' and 'fact:accepted' trees (evidence stage).</param>
    /// <param name="evidence">True for the evidence stage.</param>
    /// <param name="style">The pinned style pack; null when the Step has none.</param>
    /// <returns>The report.</returns>
    internal static LineReport RunStyled(JsonNode? scope, JsonNode? design, (JsonNode? Rounds, JsonNode? Accepted) lens, bool evidence, StylePack? style)
    {
        var c = new DesignContext(scope, design, lens.Rounds, lens.Accepted, style);
        IEnumerable<LineFinding> findings = [.. Coverage(c), .. Signals(c), .. Decisions(c), .. Citations(c), .. Contracts(c), .. StyleNotes(c)];
        if (evidence)
        {
            findings = [.. findings, .. Acceptance(c), .. Lenses(c), .. Escapes(c)];
        }

        return new LineReport(evidence, [.. findings]);
    }
}
