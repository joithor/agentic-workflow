using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Line;

/// <summary>One answer of a batch: the question, the option picked (a full id 'Q3.a' or its letter), the owner's text, or a skip.</summary>
/// <param name="Question">The question id, 'Q3' or a reserved 'Q-revise…' id.</param>
/// <param name="Option">The option picked, or null.</param>
/// <param name="Text">The free text ('text' or 'other'), or null.</param>
/// <param name="Skipped">Whether the owner skipped the question.</param>
internal sealed record AnswerEntry(string Question, string? Option, string? Text, bool Skipped)
{
    /// <summary>
    /// The entries of an answer fact or a logged batch. Two shapes exist and both are read: a batch object whose 'answers' array holds the entries
    /// (the scoping station's form and the answer log), and an array of the entries themselves (the plan station's recipe evals).
    /// </summary>
    /// <param name="node">A batch object or an array of entries.</param>
    /// <returns>The entries, in order.</returns>
    internal static IEnumerable<AnswerEntry> Of(JsonNode? node)
    {
        foreach (var item in node is JsonArray ? LineJson.Items(node) : node is null ? [] : [node])
        {
            if (LineJson.Arr(item, "answers") is { } inner)
            {
                foreach (var entry in inner.OfType<JsonNode>().Select(One).OfType<AnswerEntry>())
                {
                    yield return entry;
                }
            }
            else if (One(item) is { } entry)
            {
                yield return entry;
            }
        }
    }

    /// <summary>The entries of the latest batch: the last element when the elements are batches, else all of them (a flat batch).</summary>
    /// <param name="answers">The 'fact:answers' tree.</param>
    /// <returns>The entries.</returns>
    internal static IReadOnlyList<AnswerEntry> Latest(JsonNode? answers)
    {
        var items = LineJson.Items(answers).ToList();
        return items.Count > 0 && items.TrueForAll(static i => LineJson.Arr(i, "answers") is not null) ? [.. Of(items[^1])] : [.. Of(answers)];
    }

    private static AnswerEntry? One(JsonNode node) =>
        LineJson.Str(node, "question") is { } question
            ? new AnswerEntry(question, LineJson.Str(node, "option") ?? LineJson.Str(node, "pick"), LineJson.Str(node, "text") ?? LineJson.Str(node, "other"), LineJson.Bool(node, "skip") ?? false)
            : null;
}
