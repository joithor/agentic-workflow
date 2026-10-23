namespace Aw.Sindri.Core.Scheduling;

/// <summary>How an ended embed job left its repo (code index spec §6.3).</summary>
public enum EmbedAttemptKind
{
    /// <summary>The job failed (the model failed or a pin did not match): the backoff counts these in a row.</summary>
    Failed,

    /// <summary>The job ended because a build held the repo's index or no index existed yet: not a failure, try again shortly.</summary>
    TryLater,

    /// <summary>The job ended with the layer still pending and no more vectors than the run before it: wait long before another.</summary>
    NoProgress,

    /// <summary>The job embedded something (or finished the layer): the next one may start at once.</summary>
    Progress,
}
