using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>One row read back from a merge record.</summary>
/// <param name="Kind">The row's kind ('start', 'snapshot', 'comment', …).</param>
/// <param name="Fields">The whole row.</param>
public sealed record MergeRowRead(string Kind, JsonObject Fields);
