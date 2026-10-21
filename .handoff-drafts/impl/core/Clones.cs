using System.Buffers.Binary;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Similarity;

/// <summary>MinHash and LSH over a symbol's tokens, and the Similar decision (spec §4.2 bands, §7). Pure and deterministic: the same tokens give the same bytes on every machine.</summary>
public static class Clones
{
    private const int ShingleSize = 3;
    private const int BytesPerHash = sizeof(uint);
    private const int RowsPerBand = CodeIndexRules.MinHashLength / CodeIndexRules.MinHashBands;
    private const int SignatureBytes = CodeIndexRules.MinHashLength * BytesPerHash;
    private const string HexDigits = "0123456789abcdef";

    private static readonly ulong[] Seeds = [.. Enumerable.Range(1, CodeIndexRules.MinHashLength).Select(i => Mix((ulong)i * 0x9E3779B97F4A7C15UL))];

    /// <summary>The MinHash of a token list: <see cref="CodeIndexRules.MinHashLength"/> hashes of the token 3-grams, four bytes each, little endian.</summary>
    /// <param name="tokens">The symbol's tokens.</param>
    /// <returns>The signature, or empty under <see cref="CodeIndexRules.MinCloneTokens"/> tokens (such a symbol has no clone rows).</returns>
    public static byte[] Signature(IReadOnlyList<string> tokens)
    {
        if (tokens.Count < CodeIndexRules.MinCloneTokens)
        {
            return [];
        }

        var tokenHashes = new ulong[tokens.Count];
        for (var i = 0; i < tokenHashes.Length; i++)
        {
            tokenHashes[i] = Fnv1a.Hash(tokens[i]);
        }

        var minimums = new uint[CodeIndexRules.MinHashLength];
        Array.Fill(minimums, uint.MaxValue);
        for (var i = 0; i + ShingleSize <= tokenHashes.Length; i++)
        {
            var shingle = Mix(Mix(Mix(tokenHashes[i]) ^ tokenHashes[i + 1]) ^ tokenHashes[i + 2]);
            for (var h = 0; h < minimums.Length; h++)
            {
                minimums[h] = Math.Min(minimums[h], (uint)(Mix(shingle ^ Seeds[h]) >> 32));
            }
        }

        var signature = new byte[SignatureBytes];
        for (var h = 0; h < minimums.Length; h++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(signature.AsSpan(h * BytesPerHash), minimums[h]);
        }

        return signature;
    }

    /// <summary>The estimated Jaccard similarity of two signatures: the share of equal hashes.</summary>
    /// <param name="left">One signature.</param>
    /// <param name="right">The other.</param>
    /// <returns>0 to 1; 0 when either is empty or they differ in length.</returns>
    public static double Similarity(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != SignatureBytes || right.Length != SignatureBytes)
        {
            return 0;
        }

        var equal = 0;
        for (var h = 0; h < CodeIndexRules.MinHashLength; h++)
        {
            var at = h * BytesPerHash;
            if (left.Slice(at, BytesPerHash).SequenceEqual(right.Slice(at, BytesPerHash)))
            {
                equal++;
            }
        }

        return (double)equal / CodeIndexRules.MinHashLength;
    }

    /// <summary>The LSH band keys of a signature: <see cref="CodeIndexRules.MinHashBands"/> keys, each a band digit and four hashes in hex.</summary>
    /// <param name="signature">The signature.</param>
    /// <returns>The keys; empty for an empty signature.</returns>
    public static IReadOnlyList<string> BandKeys(ReadOnlySpan<byte> signature)
    {
        if (signature.Length != SignatureBytes)
        {
            return [];
        }

        var keys = new string[CodeIndexRules.MinHashBands];
        for (var band = 0; band < keys.Length; band++)
        {
            keys[band] = HexDigits[band] + Convert.ToHexStringLower(signature.Slice(band * RowsPerBand * BytesPerHash, RowsPerBand * BytesPerHash));
        }

        return keys;
    }

    /// <summary>Turns a reader's candidates into the answer: exact clones, and near clones at or above the threshold.</summary>
    /// <param name="subject">The symbol asked about; never in its own answer.</param>
    /// <param name="candidates">What the reader found.</param>
    /// <returns>The exact and near clones, at most <see cref="CodeIndexRules.MaxSymbolHits"/> each.</returns>
    public static SimilarClones Find(SymbolRef subject, CloneCandidates candidates)
    {
        var exact = candidates.Exact
            .Where(hit => hit.Ref.Value != subject.Value)
            .DistinctBy(hit => hit.Ref.Value, StringComparer.Ordinal)
            .OrderBy(hit => hit.Ref.Value, StringComparer.Ordinal)
            .Take(CodeIndexRules.MaxSymbolHits)
            .ToList();
        return new SimilarClones(exact, Near(subject, candidates, exact));
    }

    private static List<NearClone> Near(SymbolRef subject, CloneCandidates candidates, List<SymbolHit> exact)
    {
        var seen = new HashSet<string>(exact.Select(hit => hit.Ref.Value), StringComparer.Ordinal) { subject.Value };
        var near = new List<NearClone>();
        foreach (var candidate in candidates.Banded)
        {
            if (!seen.Add(candidate.Symbol.Ref.Value))
            {
                continue;
            }

            var similarity = Similarity(candidates.SubjectMinHash.Span, candidate.MinHash.Span);
            if (similarity >= CodeIndexRules.NearCloneThreshold)
            {
                near.Add(new NearClone(candidate.Symbol, similarity));
            }
        }

        return [.. near
            .OrderByDescending(clone => clone.Similarity)
            .ThenBy(clone => clone.Symbol.Ref.Value, StringComparer.Ordinal)
            .Take(CodeIndexRules.MaxSymbolHits)];
    }

    // The splitmix64 finalizer: a fast, well-mixed 64-bit permutation.
    private static ulong Mix(ulong value)
    {
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
