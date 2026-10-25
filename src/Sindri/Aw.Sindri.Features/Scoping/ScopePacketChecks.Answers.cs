using System.Globalization;

namespace Aw.Sindri.Features.Scoping;

internal static partial class ScopePacketChecks
{
    /// <summary>SCP-ANS-001: every batch in fact:answers is in fact:answerLog unchanged (its questions and answers), and the log's ids run A1, A2, ... in order.</summary>
    private static void AnswersLogged(ScopeContext c, RuleSink s)
    {
        if (!s.Need("answers", "answerLog"))
        {
            return;
        }

        var log = ScopeJson.Items(c.Facts["answerLog"]).ToList();
        for (var i = 0; i < log.Count; i++)
        {
            var expected = string.Create(CultureInfo.InvariantCulture, $"A{i + 1}");
            if (!string.Equals(ScopeJson.Str(log[i], "id"), expected, StringComparison.Ordinal))
            {
                s.Fail(ScopeJson.Str(log[i], "id"), $"entry {(i + 1).ToString(CultureInfo.InvariantCulture)} of the answer log has the id '{ScopeJson.Str(log[i], "id") ?? "none"}', not {expected}", "Number the log A1, A2, ... in order.");
            }
        }

        foreach (var batch in ScopeJson.Items(c.Facts["answers"]))
        {
            var found = log.Exists(entry => ScopeJson.SameValue(batch["answers"], entry["answers"]) && ScopeJson.SameValue(batch["questionIds"], entry["questionIds"]));
            if (!found)
            {
                var label = ScopeJson.Str(batch, "id") ?? string.Join(',', ScopeJson.Strings(batch, "questionIds"));
                s.Fail(label, $"the batch for {label} is in fact:answers but not, unchanged, in fact:answerLog", "Record the batch in the log (step.interview.record) before using it.");
            }
        }
    }
}
