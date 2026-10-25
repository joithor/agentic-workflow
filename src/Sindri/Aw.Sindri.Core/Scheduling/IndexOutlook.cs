namespace Aw.Sindri.Core.Scheduling;

/// <summary>Where a repo's code index stands for a held context map, as the publish gate reads it (code index spec 6.4).</summary>
public enum IndexOutlook
{
    /// <summary>The index is built for the commit the map read: ready.</summary>
    Built,

    /// <summary>A build is running or due, the index is at another commit, or its file is damaged: the map waits.</summary>
    Coming,

    /// <summary>Not built, and a retry is due later (one or two failures in a row): the map waits, bounded like <see cref="Coming"/>.</summary>
    Backoff,

    /// <summary>Not built, and three jobs failed in a row: the map does not wait.</summary>
    Broken,

    /// <summary>No index will be built, or none exists to wait for: the repo is not part of the gate.</summary>
    Absent,
}
