using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Features.LanesContext;

/// <summary>Every lane's context size.</summary>
/// <param name="RecycleAt">The threshold in tokens (context.recycleAt).</param>
/// <param name="Lanes">One row per lane, in lane order.</param>
/// <param name="Attention">Whether any lane is due for a recycle.</param>
public sealed record LanesContextResponse(int RecycleAt, IReadOnlyList<LaneContext> Lanes, bool Attention);
