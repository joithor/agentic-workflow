namespace Aw.Meta.Core;

/// <summary>The keys the meta keeps in its key-value store.</summary>
public static class KvKeys
{
    /// <summary>When the watch last polled (ISO 8601); absent until a watch has run.</summary>
    public const string WatchHeartbeat = "watch.heartbeat";

    /// <summary>The stop-check loop guard's last open set.</summary>
    public const string StopFingerprint = "stopcheck.fingerprint";

    /// <summary>How many times in a row that set blocked.</summary>
    public const string StopCount = "stopcheck.count";

    /// <summary>The watch's poll interval in seconds, so a stale heartbeat is measured against it.</summary>
    public const string WatchInterval = "watch.interval";

    /// <summary>The last health check's one-line summary.</summary>
    public const string HealthLast = "health.last";

    /// <summary>The pull requests the watch saw merged or closed on GitHub, comma separated; the merge queue leaves them out.</summary>
    public const string EndedPrs = "watch.ended-prs";
}
