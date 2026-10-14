namespace Aw.Context.Core.Results;

/// <summary>One item of a section. Every item says where it came from, whether a human confirmed it and whether its text can be trusted.</summary>
/// <param name="Kind">'record', 'feature', 'decision', 'code', …</param>
/// <param name="Node">The graph node key, or null for a plain record.</param>
/// <param name="Title">Its title, scrubbed, one line, or null.</param>
/// <param name="Text">Its text: untrusted text is scrubbed and wrapped in the untrusted fence.</param>
/// <param name="Score">Its fused score, or null.</param>
/// <param name="Why">What placed it ('rrf:repo-docs#1', 'decides→feature:…').</param>
/// <param name="Provenance">Who produced it.</param>
/// <param name="Confirmed">True when a human confirmed it (human provenance, or accepted in review).</param>
/// <param name="Trust">Whether its text can be read as instructions-free fact.</param>
/// <param name="Cites">Its citation markers.</param>
public sealed record ResultItem(string Kind, string? Node, string? Title, string? Text, double? Score, IReadOnlyList<string> Why,
    Provenance Provenance, bool Confirmed, Trust Trust, IReadOnlyList<string> Cites)
{
    /// <summary>Its cascade depth ('affects' items), or null.</summary>
    public int? Depth { get; init; }

    /// <summary>Its shortest cascade path ('affects' items), or null.</summary>
    public IReadOnlyList<string>? Path { get; init; }

    /// <summary>The estimated tokens it takes (UTF-8 characters / 4).</summary>
    public int Tokens { get; init; }
}
