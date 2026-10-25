using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// The plan station's question builder (line spec §6.1, 'questions'): the design's open decisions, as #258's question form. A decision 'D3' becomes the
/// question 'Q3' and its option 'D3.a' the option 'Q3.a', so the same number finds the decision again when the answer comes back. A decision the answer log
/// already answers is not asked twice; every other open decision goes on the form, in decision order (paging a long form is the interview Step's job).
/// </summary>
internal static class PlanQuestions
{
    /// <summary>The question id of a decision id ('D3' gives 'Q3'), or null when the id is not D followed by a number.</summary>
    internal static string? QuestionId(string? decisionId) =>
        decisionId is { Length: > 1 } && decisionId[0] == 'D' && decisionId.Skip(1).All(char.IsAsciiDigit) ? "Q" + decisionId[1..] : null;

    /// <summary>The questions to ask, or why the design's decisions cannot be asked.</summary>
    /// <param name="design">The 'fact:design' tree.</param>
    /// <param name="answerLog">The 'fact:answerLog' tree.</param>
    /// <returns>The verdict: 'asked' with the form, or 'none'.</returns>
    internal static Result<LineVerdict> Build(JsonNode? design, JsonNode? answerLog)
    {
        var answered = AnswerEntry.Of(answerLog).Select(static a => a.Question).ToHashSet(StringComparer.Ordinal);
        var questions = new JsonArray();
        foreach (var decision in LineJson.Items(design, "decisions").Where(IsOpen))
        {
            var asked = Ask(decision);
            if (asked.IsError)
            {
                return asked.Error;
            }

            if (!answered.Contains(LineJson.Str(asked.Value, "id") ?? string.Empty))
            {
                LineJson.Append(questions, asked.Value);
            }
        }

        return questions.Count == 0 ? LineVerdict.Of("none") : LineVerdict.Of("asked", ("questions", questions));
    }

    private static bool IsOpen(JsonNode decision) => LineJson.Str(decision, "status") is not { } status || string.Equals(status, "open", StringComparison.Ordinal);

    private static Result<JsonNode> Ask(JsonNode decision)
    {
        var id = LineJson.Str(decision, "id") ?? string.Empty;
        var options = LineJson.Items(decision, "options").ToList();
        if (QuestionId(id) is not { } question)
        {
            return SindriErrors.InvalidInput.With($"the decision id '{id}' is not D<number> (fix: number decisions D1, D2 …)");
        }

        if (options.Count is < 2 or > 4 || options.Where(static o => LineJson.Bool(o, "recommended") == true).Take(2).Count() != 1)
        {
            return SindriErrors.InvalidInput.With($"decision {id} needs two to four options and exactly one recommended (DSN-DEC-001) before it can be asked");
        }

        var form = new JsonArray();
        foreach (var option in options)
        {
            if (Option(option, id, question, LineJson.ToArray(LineJson.Strings(decision, "cites"))) is not { } entry)
            {
                return SindriErrors.InvalidInput.With($"an option of decision {id} has the id '{LineJson.Str(option, "id") ?? "none"}', not {id}.<letter>");
            }

            LineJson.Append(form, entry);
        }

        return Results.Success<JsonNode>(new JsonObject { ["id"] = question, ["question"] = LineJson.Str(decision, "question") ?? string.Empty, ["options"] = form, ["other"] = true, ["skip"] = true });
    }

    private static JsonObject? Option(JsonNode option, string decisionId, string question, JsonArray cites)
    {
        var optionId = LineJson.Str(option, "id") ?? string.Empty;
        if (!optionId.StartsWith(decisionId + ".", StringComparison.Ordinal) || optionId.Length != decisionId.Length + 2 || !char.IsAsciiLetterLower(optionId[^1]))
        {
            return null;
        }

        var entry = new JsonObject
        {
            ["id"] = $"{question}.{optionId[^1].ToString(CultureInfo.InvariantCulture)}",
            ["answer"] = LineJson.Str(option, "answer") ?? string.Empty,
            ["rationale"] = LineJson.Str(option, "tradeoffs") ?? string.Empty,
            ["cites"] = cites.DeepClone(),
        };
        if (LineJson.Bool(option, "recommended") == true)
        {
            entry["recommended"] = true;
        }

        return entry;
    }
}
