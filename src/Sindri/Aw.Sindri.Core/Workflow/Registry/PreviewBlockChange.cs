using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>A pinned block that changed since approval, as a drift approval's preview shows it (spec §4.1), with its capability diff.</summary>
/// <param name="Block">The block.</param>
/// <param name="Approved">The descriptor hash the approval pinned.</param>
/// <param name="Build">This build's descriptor hash.</param>
/// <param name="Added">Capabilities the build's descriptor has and the pinned one hadn't.</param>
/// <param name="Removed">Capabilities the pinned descriptor had and the build's hasn't.</param>
public sealed record PreviewBlockChange(BlockId Block, DescriptorHash Approved, DescriptorHash Build, Capability Added, Capability Removed);
