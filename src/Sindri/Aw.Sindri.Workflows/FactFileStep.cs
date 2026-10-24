using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What decides a Step's fact files besides the facts: its descriptor and its 'subject' parameter.</summary>
/// <param name="Descriptor">The Step's descriptor; null when the catalog doesn't know it (no files).</param>
/// <param name="Subject">The Step's 'subject' parameter ('design' or 'change'); null when it has none.</param>
internal sealed record FactFileStep(BlockDescriptor? Descriptor, string? Subject);
