using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>One thing a desk Step has its person review, declared by the block: the fact that holds it and what kind of thing it is.</summary>
/// <param name="Fact">The fact the instance holds it in.</param>
/// <param name="Kind">What it is.</param>
public sealed record ReviewSpec(FactKey Fact, DeskReviewKind Kind);
