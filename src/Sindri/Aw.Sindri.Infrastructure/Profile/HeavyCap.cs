using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>The box's heavy-slot cap, as the heavy wrapper reads it (blocks-over-MCP spec BM-1): the cap file $AW_STATE_DIR/heavy-slots-cap wins over AW_HEAVY_SLOTS.</summary>
internal static class HeavyCap
{
    /// <summary>The cap now.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="stateRoot">The state root (AW_STATE_DIR or ~/.agentic-workflow).</param>
    internal static BoxLimits Read(Func<string, string?> environment, string stateRoot) => throw new NotSupportedException();
}
