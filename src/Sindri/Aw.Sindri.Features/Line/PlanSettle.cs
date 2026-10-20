using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// The plan station's settle (line spec §6.1, 'settle'): the latest answer batch against the design's assumptions. 'assumed' when every pick is the recommended
/// option (or skipped); 'changed' when a pick differs, an "other" text answers a decision, or the stop sent a design revision note (it writes
/// 'fact:designRevision'); 'replan' for a plan revision note alone. A design change wins over a plan note, since a redesign re-plans anyway.
/// </summary>
internal static class PlanSettle
{
    // A revision with no words still has to read as a revision to the design intake, which looks for decisions or notes.
    private const string NoRevisionText = "owner asked for a revision, no text";

    /// <summary>Settles the latest batch.</summary>
    /// <param name="design">The 'fact:design' tree.</param>
    /// <param name="answers">The 'fact:answers' tree.</param>
    /// <returns>The verdict, or why an answer cannot be matched.</returns>
    internal static Result<LineVerdict> Run(JsonNode? design, JsonNode? answers)
    {
        var decisions = new JsonArray();
        var notes = new List<string>();
        var planNote = false;
        var designNote = false;
        foreach (var entry in AnswerEntry.Latest(answers).Where(static a => !a.Skipped))
        {
            var kind = Note(entry.Question);
            if (kind != NoteKind.None)
            {
                planNote |= kind == NoteKind.Plan;
                designNote |= kind == NoteKind.Design;
                if (kind == NoteKind.Design)
                {
                    notes.Add(entry.Text is { Length: > 0 } text ? text : NoRevisionText);
                }

                continue;
            }

            var changed = Change(design, entry);
            if (changed.IsError)
            {
                return changed.Error;
            }

            if (changed.Value is { } change)
            {
                LineJson.Append(decisions, change);
            }
        }

        return decisions.Count > 0 || designNote
            ? LineVerdict.Of("changed", ("designRevision", new JsonObject { ["decisions"] = decisions, ["notes"] = LineJson.ToArray(notes) }))
            : LineVerdict.Of(planNote ? "replan" : "assumed");
    }

    private static NoteKind Note(string question) =>
        string.Equals(question, "Q-revise-plan", StringComparison.Ordinal) ? NoteKind.Plan
        : string.Equals(question, "Q-revise-design", StringComparison.Ordinal) ? NoteKind.Design
        : NoteKind.None;

    // null: the answer keeps the assumption. A node: the decision it changes.
    private static Result<JsonNode?> Change(JsonNode? design, AnswerEntry entry)
    {
        var decision = LineJson.Items(design, "decisions").FirstOrDefault(d => string.Equals(PlanQuestions.QuestionId(LineJson.Str(d, "id")), entry.Question, StringComparison.Ordinal));
        if (decision is null)
        {
            return SindriErrors.InvalidInput.With($"the answer to {entry.Question} matches no decision of the design (fix: answer only the questions the plan station asked)");
        }

        var id = LineJson.Str(decision, "id") ?? string.Empty;
        if (entry.Option is null)
        {
            return entry.Text is { Length: > 0 } other
                ? Results.Success<JsonNode?>(value: new JsonObject { ["decision"] = id, ["text"] = other })
                : Results.Success<JsonNode?>(value: null);
        }

        var letter = entry.Option[(entry.Option.LastIndexOf('.') + 1)..];
        var picked = LineJson.Items(decision, "options").FirstOrDefault(o => string.Equals(LineJson.Str(o, "id"), $"{id}.{letter}", StringComparison.Ordinal));
        if (picked is null)
        {
            return SindriErrors.InvalidInput.With($"the answer to {entry.Question} picks '{entry.Option}', which is not an option of decision {id}");
        }

        return LineJson.Bool(picked, "recommended") == true
            ? Results.Success<JsonNode?>(value: null)
            : Results.Success<JsonNode?>(value: new JsonObject { ["decision"] = id, ["option"] = $"{id}.{letter}" });
    }
}
