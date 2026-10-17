using Aw.Kernel;

namespace Aw.Sindri.Core.Sessions;

/// <summary>The one place a reason becomes SND-SESSION-002's fixed text and back; any other error is not a reason.</summary>
public static class UnavailableReasons
{
    /// <summary>The fixed text of <paramref name="reason"/>.</summary>
    /// <param name="reason">The reason.</param>
    /// <returns>'unsupported-platform', 'proc-unreadable' or 'disabled'.</returns>
    public static string Text(UnavailableReason reason) => reason switch
    {
        UnavailableReason.UnsupportedPlatform => "unsupported-platform",
        UnavailableReason.ProcUnreadable => "proc-unreadable",
        UnavailableReason.Disabled => "disabled",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    /// <summary>SND-SESSION-002 for <paramref name="reason"/>.</summary>
    /// <param name="reason">The reason.</param>
    /// <returns>The error.</returns>
    public static Error ToError(UnavailableReason reason) => SindriErrors.SessionsUnavailable.With(Text(reason));

    /// <summary>The reason an SND-SESSION-002 error carries; false for any other error (a cancelled read, say).</summary>
    /// <param name="error">The error.</param>
    /// <param name="reason">The reason, when it is one.</param>
    /// <returns>True when <paramref name="error"/> is SND-SESSION-002 with a known reason.</returns>
    public static bool TryFrom(Error error, out UnavailableReason reason)
    {
        ArgumentNullException.ThrowIfNull(error);
        foreach (var candidate in Enum.GetValues<UnavailableReason>())
        {
            if (string.Equals(error.Code, SindriErrors.SessionsUnavailable.Code, StringComparison.Ordinal) && string.Equals(error.Message, Text(candidate), StringComparison.Ordinal))
            {
                reason = candidate;
                return true;
            }
        }

        reason = default;
        return false;
    }
}
