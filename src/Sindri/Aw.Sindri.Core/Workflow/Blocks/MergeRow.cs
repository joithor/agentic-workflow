using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>One row of the merge record: the fixed columns every row carries and the fields its kind adds (PB spec §4.7).</summary>
/// <param name="instance">The instance's address.</param>
/// <param name="identity">The writer's identity, what keeps a retry from writing a second row: '&lt;instance&gt;:&lt;transition sequence&gt;:&lt;index&gt;' for an effect, the instance and the previous record for a Step.</param>
/// <param name="kind">The row's kind: start, snapshot, push, comment, recheck, docs, restack, merge, docsMerge or end.</param>
/// <param name="headSha">The PR head the row is about, or null.</param>
/// <param name="fields">The fields the kind adds, written beside the fixed columns; a fixed column's name in it is ignored.</param>
public sealed class MergeRow(string instance, string identity, string kind, string? headSha, JsonObject fields)
{
    /// <summary>The instance's address.</summary>
    public string Instance { get; } = instance;

    /// <summary>What keeps a retried writer from writing a second row.</summary>
    public string Identity { get; } = identity;

    /// <summary>The row's kind.</summary>
    public string Kind { get; } = kind;

    /// <summary>The PR head the row is about, or null.</summary>
    public string? HeadSha { get; } = headSha;

    /// <summary>The fields the kind adds.</summary>
    public JsonObject Fields { get; } = fields;
}
