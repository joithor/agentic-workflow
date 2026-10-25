using System.Globalization;

namespace Aw.Meta.Core.Usage;

/// <summary>The limited subjects a watch has announced, kept between watches so a re-armed one does not repeat itself.</summary>
/// <param name="Entries">The entries by subject.</param>
internal sealed record UsageMemory(IReadOnlyDictionary<string, UsageEntry> Entries)
{
    private const char Separator = '\t';
    private const string None = "-";

    /// <summary>Nothing announced.</summary>
    internal static UsageMemory Empty { get; } = new(new Dictionary<string, UsageEntry>(StringComparer.Ordinal));

    /// <summary>Reads the stored text.</summary>
    /// <param name="text">What <see cref="Format"/> wrote; null or damaged text reads as empty.</param>
    /// <returns>The memory.</returns>
    internal static UsageMemory Parse(string? text)
    {
        var entries = new Dictionary<string, UsageEntry>(StringComparer.Ordinal);
        foreach (var line in (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (ParseLine(line) is var (subject, entry))
            {
                entries[subject] = entry;
            }
        }

        return new UsageMemory(entries);
    }

    /// <summary>Writes the memory as text.</summary>
    /// <returns>One line per subject.</returns>
    internal string Format() =>
        string.Join(
            '\n',
            Entries.OrderBy(static e => e.Key, StringComparer.Ordinal).Select(static e =>
                string.Join(Separator, e.Key, e.Value.Key, e.Value.Since.ToString("O", CultureInfo.InvariantCulture), e.Value.OkAt?.ToString("O", CultureInfo.InvariantCulture) ?? None, e.Value.Resumed ? "1" : "0")));

    private static (string Subject, UsageEntry Entry)? ParseLine(string line)
    {
        var parts = line.Split(Separator);
        if (parts.Length != 5 || parts[0].Length == 0 || parts[1].Length == 0 || Time(parts[2]) is not { } since)
        {
            return null;
        }

        var ok = string.Equals(parts[3], None, StringComparison.Ordinal) ? null : Time(parts[3]);
        var resumed = string.Equals(parts[4], "1", StringComparison.Ordinal);
        return ok is null && !string.Equals(parts[3], None, StringComparison.Ordinal) ? null : (parts[0], new UsageEntry(parts[1], since, ok, resumed));
    }

    private static DateTimeOffset? Time(string text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;
}
