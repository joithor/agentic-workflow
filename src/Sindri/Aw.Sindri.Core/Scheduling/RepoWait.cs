namespace Aw.Sindri.Core.Scheduling;

/// <summary>The per-repo word of a held map's wait (the owner's vocabulary, code index spec 6.4).</summary>
public enum RepoWait
{
    /// <summary>Nothing to say: built, or a repo the gate does not look at.</summary>
    None,

    /// <summary>A build is due and not started yet.</summary>
    Queued,

    /// <summary>A build is running.</summary>
    Building,

    /// <summary>The last build failed; a retry is due later.</summary>
    Retrying,

    /// <summary>The index is at another commit and the repo's head has moved past the map's: the map waits for the next one.</summary>
    NewerIndex,

    /// <summary>The index file is damaged and will be rebuilt.</summary>
    Damaged,

    /// <summary>The repo's first index is not built yet; it is never waited for.</summary>
    NotIndexed,
}
