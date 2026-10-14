using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>One effect kind an approval preview lists (spec §4.2), with whether the profile's effect allowlist holds it.</summary>
/// <param name="Kind">The effect block.</param>
/// <param name="Allowlisted">True when 'workflow.effects' allows it; false when it doesn't or the key is absent (S1-D11).</param>
public sealed record PreviewEffect(BlockId Kind, bool Allowlisted);
