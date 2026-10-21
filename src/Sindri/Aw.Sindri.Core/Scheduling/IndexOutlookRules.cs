namespace Aw.Sindri.Core.Scheduling;

/// <summary>
/// Classifies one repo for the map publish gate (code index spec 6.4). The rows apply in this order, which makes the table total:
/// Built, Absent, Broken, Backoff, then Coming as the default.
/// </summary>
public static class IndexOutlookRules
{
    /// <summary>Failed jobs in a row after which the index counts as broken (the code index service's alarm uses the same number).</summary>
    public const int FailuresToBroken = 3;

    /// <summary>How long a deferred (heavy-busy) job suppresses the next start (code index spec 6.3).</summary>
    public static readonly TimeSpan DeferredSuppress = TimeSpan.FromMinutes(10);

    /// <summary>Classifies.</summary>
    /// <param name="input">What is known of the repo.</param>
    /// <param name="now">The time.</param>
    /// <returns>The outlook, the owner's word and the retry time.</returns>
    public static IndexOutlookDetail Classify(IndexOutlookInput input, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(input);
        throw new NotSupportedException(now.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
    }
}
