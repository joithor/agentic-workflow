namespace Aw.Packets.Core.Kinds.Line;

/// <summary>
/// One line kind's rule ids: a prefix, and the numbers 001 to 006 every line kind shares. A kind's own rules continue
/// from 007 (see <see cref="LineRules"/>). They are schema rules only: what the files mean stays in Sindri's checks.
/// </summary>
/// <param name="Prefix">The id prefix, for example 'PKT-DSN'.</param>
public sealed record LineRuleSet(string Prefix)
{
    /// <summary>Every file the kind lists is present.</summary>
    public string FilesPresent => Id(1);

    /// <summary>The directory holds no entry the kind doesn't list.</summary>
    public string NoUnlisted => Id(2);

    /// <summary>Every JSON file is one strict JSON object, every JSON-lines file holds one object per line, every text file is UTF-8.</summary>
    public string WellFormed => Id(3);

    /// <summary>packet.json matches the kind's schema, says the kind, and holds a full-length id.</summary>
    public string Manifest => Id(4);

    /// <summary>packet.json's links name each of the kind's files, and index.json, and nothing else.</summary>
    public string Links => Id(5);

    /// <summary>index.json matches its schema and says the kind.</summary>
    public string Index => Id(6);

    /// <summary>The rule numbered <paramref name="number"/> for this kind.</summary>
    /// <param name="number">1 or more.</param>
    /// <returns>For example 'PKT-DSN-007'.</returns>
    public string Id(int number) => $"{Prefix}-{number.ToString("000", System.Globalization.CultureInfo.InvariantCulture)}";
}
