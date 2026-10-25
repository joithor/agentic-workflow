using System.Text.RegularExpressions;

namespace Aw.Sindri.Features.Line;

internal static partial class DesignChecks
{
    // DSN-CITE-001: scope ids (S-, AC-, A, R, L-) or a decision of the design.
    private static IEnumerable<LineFinding> Citations(DesignContext c)
    {
        foreach (var section in c.Sections)
        {
            var id = LineJson.Str(section, "id") ?? "?";
            var cites = LineJson.Strings(section, "cites").ToList();
            if (cites.Count == 0)
            {
                yield return DesignContext.Fail("DSN-CITE-001", id, $"section {id} cites no scope id or decision", "Cite the scope surfaces, acceptance items, answers or decisions it rests on.");
            }

            foreach (var cite in cites.Where(x => !Resolves(c, x)))
            {
                yield return DesignContext.Fail("DSN-CITE-001", id, $"section {id} cites {cite}, which is no scope id and no decision of the design", "Cite an id that exists in the scope packet or the design.");
            }
        }
    }

    private static bool Resolves(DesignContext c, string cite) =>
        ScopeId.IsMatch(cite) || c.IsDesignId(cite) || c.AnswerIds.Contains(cite, StringComparer.Ordinal);

    [GeneratedRegex("^(S[1-9][0-9]*|AC-[1-9][0-9]*|R[1-9][0-9]*|L-(code|product|related|history)-[1-9][0-9]*)$", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ScopeId { get; }
}
