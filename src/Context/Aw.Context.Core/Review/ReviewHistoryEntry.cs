using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Review;

/// <summary>One override in the history (the ids "map review --undo" takes).</summary>
/// <param name="Id">The override's id.</param>
/// <param name="Action">What it did.</param>
/// <param name="Target">The node it acts on.</param>
/// <param name="Link">The link it decides, or null.</param>
/// <param name="Label">Its label, or null.</param>
/// <param name="Who">Who wrote it.</param>
/// <param name="At">When.</param>
/// <param name="Undone">True when a later undo cancelled it.</param>
/// <param name="Undoes">The override this row undoes, or null.</param>
public sealed record ReviewHistoryEntry(OverrideId Id, OverrideAction Action, NodeKey Target, LinkId? Link, string? Label, string Who, DateTimeOffset At, bool Undone, OverrideId? Undoes);
