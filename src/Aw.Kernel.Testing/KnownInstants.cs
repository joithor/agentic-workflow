
using System.Globalization;

namespace Aw.Kernel.Testing;

/// <summary>
/// Fixed instants to start a 'FakeTimeProvider' at. Test projects take 'FakeTimeProvider' from
/// Microsoft.Extensions.TimeProvider.Testing directly: Aw.Kernel.Testing keeps to one external dependency
/// (spec §4b.1), and a copy of that fake would drift from the maintained one.
/// </summary>
public static class KnownInstants
{
    /// <summary>2026-01-01T00:00:00Z, the default start of every test clock.</summary>
    public static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly string[] _utcFormats = ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"];

    /// <summary>Parses a UTC instant written as in a golden file ('yyyy-MM-ddTHH:mm:ss[.fff]Z').</summary>
    /// <param name="utc">The instant; it must end in 'Z'.</param>
    /// <returns>The instant.</returns>
    public static DateTimeOffset Parse(string utc)
    {
        ArgumentNullException.ThrowIfNull(utc);
        return DateTimeOffset.ParseExact(
            utc, _utcFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
}
