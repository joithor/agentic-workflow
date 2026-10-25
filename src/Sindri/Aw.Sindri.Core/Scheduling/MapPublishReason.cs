namespace Aw.Sindri.Core.Scheduling;

/// <summary>Why a held map goes ahead.</summary>
public enum MapPublishReason
{
    /// <summary>Every repo the gate looks at has its index at the map's commit.</summary>
    IndexReady,

    /// <summary>No repo is part of the gate: there is no code index to wait for.</summary>
    NoIndex,

    /// <summary>An index has given up (three failed jobs in a row): waiting buys nothing.</summary>
    IndexBroken,

    /// <summary>No map has been promoted yet: the first map is worth more than a matching one.</summary>
    FirstMap,

    /// <summary>Waiting is turned off (context.publish.waitForIndexMinutes is 0).</summary>
    WaitOff,

    /// <summary>The wait ended before the index caught up: the map goes ahead and its code rows read stale.</summary>
    TimedOut,
}
