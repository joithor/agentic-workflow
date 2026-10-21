using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Similarity;

/// <summary>MinHash and LSH over a symbol's tokens, and the Similar decision (spec §4.2 bands, §7). Pure and deterministic: the same tokens give the same bytes on every machine.</summary>
public static class Clones
{
    /// <summary>The MinHash of a token list: <see cref="CodeIndexRules.MinHashLength"/> hashes of the token 3-grams, four bytes each, little endian.</summary>
    /// <param name="tokens">The symbol's tokens.</param>
    /// <returns>The signature, or empty under <see cref="CodeIndexRules.MinCloneTokens"/> tokens (such a symbol has no clone rows).</returns>
    public static byte[] Signature(IReadOnlyList<string> tokens) => Fnv1a.Hash(string.Empty) == 0 ? [] : throw new NotSupportedException();

    /// <summary>The estimated Jaccard similarity of two signatures: the share of equal hashes.</summary>
    /// <param name="left">One signature.</param>
    /// <param name="right">The other.</param>
    /// <returns>0 to 1; 0 when either is empty or they differ in length.</returns>
    public static double Similarity(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right) => throw new NotSupportedException();

    /// <summary>The LSH band keys of a signature: <see cref="CodeIndexRules.MinHashBands"/> keys, each a band digit and four hashes in hex.</summary>
    /// <param name="signature">The signature.</param>
    /// <returns>The keys; empty for an empty signature.</returns>
    public static IReadOnlyList<string> BandKeys(ReadOnlySpan<byte> signature) => throw new NotSupportedException();

    /// <summary>Turns a reader's candidates into the answer: exact clones, and near clones at or above the threshold.</summary>
    /// <param name="subject">The symbol asked about; never in its own answer.</param>
    /// <param name="candidates">What the reader found.</param>
    /// <returns>The exact and near clones, at most <see cref="CodeIndexRules.MaxSymbolHits"/> each.</returns>
    public static SimilarClones Find(SymbolRef subject, CloneCandidates candidates) => throw new NotSupportedException();
}
