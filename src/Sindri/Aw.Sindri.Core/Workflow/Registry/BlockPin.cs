using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>One pinned block (W11): the descriptor hash of a block the recipe names, as the approval saw it.</summary>
/// <param name="Block">The block id.</param>
/// <param name="Hash">Its descriptor hash at approval.</param>
public sealed record BlockPin(BlockId Block, DescriptorHash Hash);
