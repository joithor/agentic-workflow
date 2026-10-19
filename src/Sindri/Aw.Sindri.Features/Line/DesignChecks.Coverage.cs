namespace Aw.Sindri.Features.Line;

internal static partial class DesignChecks
{
    // DSN-COV-001, DSN-COV-002
    private static IEnumerable<LineFinding> Coverage(DesignContext c)
    {
        var covered = c.Sections.SelectMany(static s => LineJson.Strings(s, "covers")).ToHashSet(StringComparer.Ordinal);
        var excluded = LineJson.Items(LineJson.Obj(c.Design, "coverage"), "excluded").Where(static e => !string.IsNullOrWhiteSpace(LineJson.Str(e, "reason"))).Select(static e => LineJson.Str(e, "surface")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var surface in c.SurfaceIds.Where(s => !covered.Contains(s) && !excluded.Contains(s)))
        {
            yield return DesignContext.Fail("DSN-COV-001", surface, $"scope surface {surface} is covered by no section and not excluded with a reason", "Add it to a section's covers, or list it in coverage.excluded with a one-line reason.");
        }

        var answeredBy = c.Sections.Concat(c.Decisions).SelectMany(static n => LineJson.Strings(n, "answers")).ToHashSet(StringComparer.Ordinal);
        var deferred = LineJson.Items(c.Design, "deferred").Where(static d => !string.IsNullOrWhiteSpace(LineJson.Str(d, "owner")) && !string.IsNullOrWhiteSpace(LineJson.Str(d, "buildMayAssume"))).Select(static d => LineJson.Str(d, "question")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var question in c.OpenQuestionIds.Where(q => !answeredBy.Contains(q) && !deferred.Contains(q)))
        {
            yield return DesignContext.Fail("DSN-COV-002", question, $"scope open question {question} is not answered by a section, carried as a decision, or deferred with an owner and a buildMayAssume default", "Answer it in a section, record an open decision for it, or defer it with owner and buildMayAssume.");
        }
    }
}
