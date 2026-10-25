using Aw.Context.Core.Walks;

namespace Aw.Context.Features;

/// <summary>The degree the engine's read-only walks run at unless a caller sets another: every core but one, so the rest of the box keeps a core.</summary>
internal static class WalkDefaults
{
    internal static WalkOptions Default { get; } = WalkOptions.Of(Math.Max(1, Environment.ProcessorCount - 1));
}
