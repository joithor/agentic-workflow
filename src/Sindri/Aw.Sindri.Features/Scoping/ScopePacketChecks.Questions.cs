using System.Globalization;

namespace Aw.Sindri.Features.Scoping;

internal static partial class ScopePacketChecks
{
    /// <summary>SCP-QST-001: every interview question has two or three options and exactly one of them recommended, like a design decision (DSN-DEC-001); the owner's fastest path is the recommended option, and a delegated run can only follow one.</summary>
    private static void QuestionsRecommended(ScopeContext c, RuleSink s)
    {
        if (!s.Need("questions"))
        {
            return;
        }

        foreach (var question in ScopeJson.Items(c.Facts["questions"]))
        {
            var id = ScopeJson.Str(question, "id") ?? "?";
            var options = ScopeJson.Items(question, "options").ToList();
            if (options.Count is < 2 or > 3)
            {
                s.Fail(id, $"{id} has {options.Count} options", "Give it two or three options.");
            }

            var recommended = options.Count(static o => ScopeJson.Bool(o, "recommended") == true);
            if (recommended != 1)
            {
                s.Fail(id, string.Create(CultureInfo.InvariantCulture, $"{id} has {recommended} recommended options"), "Mark exactly one option recommended; for a question that can't be decided yet, recommend the option that defers and reports it, or move it to openQuestions.");
            }
        }
    }
}
