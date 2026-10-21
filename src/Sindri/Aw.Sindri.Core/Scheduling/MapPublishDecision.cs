namespace Aw.Sindri.Core.Scheduling;

/// <summary>When a refreshed context map becomes the current one (owner decision 2026-10-20, code index spec 6.4): clock in, nothing out.</summary>
public static class MapPublishDecision
{
    /// <summary>Decides, first applicable line wins.</summary>
    /// <param name="input">The hold, the draft and current numbers, the outlooks and the wait.</param>
    /// <param name="now">The time.</param>
    /// <returns>Wait, publish or drop.</returns>
    public static MapPublishAction Decide(MapPublishInput input, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(input);
        throw new NotSupportedException(now.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
    }
}
