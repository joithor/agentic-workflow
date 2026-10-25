namespace Aw.Sindri.Core.Scheduling;

/// <summary>Why a hold is dropped without publishing.</summary>
public enum MapDropReason
{
    /// <summary>The current version is the held one (a defensive guard: a promote deletes the hold in its own transaction).</summary>
    AlreadyCurrent,

    /// <summary>The latest draft is not the held one: a rebuild replaced it, or a person cleared it.</summary>
    Gone,
}
