using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>Adds up the merge records' 'comment' and 'docsMerge' rows and their unreadable lines.</summary>
internal sealed class BabysitterRecordTally
{
    private readonly Dictionary<string, Dictionary<string, int>> _comments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _docs = new(StringComparer.Ordinal);

    /// <summary>Unreadable lines.</summary>
    public int Errors { get; private set; }

    /// <summary>Takes one instance's record in.</summary>
    /// <param name="record">The record.</param>
    public void Add(MergeRecordRows record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Errors += record.Errors;
        foreach (var row in record.Rows)
        {
            if (string.Equals(row.Kind, BabysitterNames.CommentRow, StringComparison.Ordinal))
            {
                Comment(Text(row, "type") ?? "none", Text(row, "outcome") ?? "unknown");
            }
            else if (string.Equals(row.Kind, BabysitterNames.DocsMergeRow, StringComparison.Ordinal))
            {
                _docs.Increment(Text(row, "outcome") ?? "unknown");
            }
        }
    }

    /// <summary>The comment figures.</summary>
    /// <returns>The totals and the rows by type.</returns>
    public BabysitterCommentStats Comments()
    {
        var byType = _comments.OrderBy(static t => t.Key, StringComparer.Ordinal)
            .Select(static t => new CommentTypeStats(t.Key, Of(t.Value, "fixed"), Of(t.Value, "replied"), Of(t.Value, "reacted"), Of(t.Value, BabysitterNames.HeldOutcome), Of(t.Value, "skipped")))
            .ToList();
        var total = _comments.Values.Sum(static o => o.Values.Sum());
        var held = _comments.Values.Sum(static o => Of(o, BabysitterNames.HeldOutcome));
        return new BabysitterCommentStats(total, held, total - held, byType);
    }

    /// <summary>The docs merges' outcomes, the commonest first.</summary>
    /// <returns>The outcomes and their counts.</returns>
    public IReadOnlyList<ReasonCount> DocsOutcomes() =>
        [.. _docs.OrderByDescending(static d => d.Value).ThenBy(static d => d.Key, StringComparer.Ordinal).Select(static d => new ReasonCount(d.Key, d.Value))];

    private void Comment(string type, string outcome)
    {
        if (!_comments.TryGetValue(type, out var outcomes))
        {
            _comments[type] = outcomes = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        outcomes.Increment(outcome);
    }

    private static int Of(Dictionary<string, int> outcomes, string outcome) => outcomes.GetValueOrDefault(outcome);

    private static string? Text(MergeRowRead row, string name) =>
        row.Fields[name] is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;
}
