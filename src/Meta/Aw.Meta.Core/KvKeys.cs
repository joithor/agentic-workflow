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
}
