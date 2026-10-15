using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Map;

/// <summary>Who gets clean text and who gets the fence (provisional owner default, 2026-10-14 night; spec §7 egress).</summary>
public static class FencePolicies
{
    /// <summary>The policy for a caller.</summary>
    /// <param name="caller">The caller.</param>
    /// <returns>Clean for a human at the CLI or over plain HTTP; fenced for everyone else.</returns>
    public static FencePolicy For(ContextCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        return FencePolicy.Fenced;
    }
}
