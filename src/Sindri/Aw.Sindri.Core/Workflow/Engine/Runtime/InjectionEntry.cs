using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>One section of a Step's injection plan, by metadata only: the text is never stored (plan decision 2, E3).</summary>
/// <param name="Section">The section name.</param>
/// <param name="Input">The input block that filled it.</param>
/// <param name="Bytes">The UTF-8 size of the scrubbed text.</param>
/// <param name="Sha256">The SHA-256 of the scrubbed text, in lower-case hex.</param>
/// <param name="Replaced">The earlier input whose section of the same name this one replaced; null when it replaced none.</param>
public sealed record InjectionEntry(string Section, BlockId Input, int Bytes, string Sha256, BlockId? Replaced = null);
