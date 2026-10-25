using System.Globalization;

namespace Aw.Sindri.Features.Line;

internal static partial class DesignChecks
{
    // DSN-XSPEC-003, and the warnings for XSPEC-001/-002 (they need the repo at the base sha) and for STYLE-001 when no style pack was passed.
    private static IEnumerable<LineFinding> Contracts(DesignContext c)
    {
        var edits = LineJson.Items(c.Design, "contracts").ToList();
        foreach (var edit in edits.Where(static e => (LineJson.Int(e, "row") ?? 0) < 1 || string.IsNullOrWhiteSpace(LineJson.Str(e, "lane"))))
        {
            yield return DesignContext.Fail("DSN-XSPEC-003", LineJson.Str(edit, "id"), "a contract edit is not filed as a numbered row for one lane", "Give it a row number and its lane in the 'Rows to other lanes' section.");
        }

        foreach (var row in edits.GroupBy(static e => LineJson.Int(e, "row")).Where(static g => g.Select(static e => LineJson.Str(e, "lane")).Distinct(StringComparer.Ordinal).Skip(1).Any()))
        {
            var key = row.Key?.ToString(CultureInfo.InvariantCulture) ?? "none";
            yield return DesignContext.Fail("DSN-XSPEC-003", $"row {key}", $"row {key} holds edits for more than one lane", "File one row per lane.");
        }

        foreach (var same in edits.GroupBy(static e => (LineJson.Str(LineJson.Obj(e, "target"), "file"), LineJson.Str(e, "quote"))).Where(static g => g.Skip(1).Any()))
        {
            yield return DesignContext.Fail("DSN-XSPEC-003", same.Key.Item1, "two edits change the same quoted span", "Merge the edits into one row.");
        }

        if (edits.Count > 0)
        {
            yield return DesignContext.Warning("DSN-XSPEC-001", item: null, "the target files, headings and quotes of the contract edits were not checked against the base sha on this runtime (DSN-XSPEC-001, -002)", "Check them with sindri design check at the desk.");
        }
    }

    private static IEnumerable<LineFinding> StyleNotes(DesignContext c)
    {
        if (c.Style is { } style)
        {
            foreach (var finding in StyleFindings(c, style))
            {
                yield return finding;
            }
        }
        else if (c.Sections.Count > 0)
        {
            yield return DesignContext.Warning("DSN-STYLE-001", item: null, "spec.md was not checked against the style pack's section rules on this runtime", "Check it with sindri design check at the desk.");
        }
    }
}
