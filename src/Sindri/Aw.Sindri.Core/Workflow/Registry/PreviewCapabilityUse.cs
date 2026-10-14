using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>One capability use an approval preview lists (spec §4.2): the block, the transition it sits on, its capabilities and its capability parameters.</summary>
/// <param name="Block">The capability-bearing block.</param>
/// <param name="From">The transition's source state.</param>
/// <param name="To">The transition's target state.</param>
/// <param name="Capabilities">The block's capabilities.</param>
/// <param name="Parameters">Its capability parameters as 'name=value'.</param>
public sealed record PreviewCapabilityUse(BlockId Block, string From, string To, Capability Capabilities, IReadOnlyList<string> Parameters);
