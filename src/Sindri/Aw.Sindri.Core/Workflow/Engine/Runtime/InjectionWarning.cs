using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>A later input's section replaced an earlier one's of the same name.</summary>
/// <param name="Section">The section name.</param>
/// <param name="Replaced">The input whose section was replaced.</param>
/// <param name="By">The input whose section won.</param>
public sealed record InjectionWarning(string Section, BlockId Replaced, BlockId By);
