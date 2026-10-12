using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Graph;

/// <summary>
/// A decision in Sindri's wording (spec §6.1). Its key hashes the quoted record and the quote, so rewording keeps the key
/// and its overrides. The record it quotes is also its 'quotes' edge; what it decides and supersedes are edges only.
/// </summary>
/// <param name="Record">The quoted record's canonical key.</param>
/// <param name="Quote">The verbatim quote.</param>
/// <param name="Wording">Sindri's wording.</param>
/// <param name="Provenance">Who wrote the wording.</param>
/// <param name="At">When it was written; an import keeps the file's value.</param>
public sealed record DecisionRow(RecordKey Record, string Quote, string Wording, Provenance Provenance = Provenance.Human, DateTimeOffset? At = null)
{
    /// <summary>The decision's node key: 'decision:' and the SHA-256 of the record key, a newline and the quote.</summary>
    public NodeKey Key => KeyFor(Record, Quote);

    /// <summary>The key a decision quoting <paramref name="quote"/> from <paramref name="record"/> gets.</summary>
    /// <param name="record">The record.</param>
    /// <param name="quote">The quote.</param>
    /// <returns>The key.</returns>
    public static NodeKey KeyFor(RecordKey record, string quote) =>
        NodeKey.From("decision:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{record.Value}\n{quote}"))));
}
