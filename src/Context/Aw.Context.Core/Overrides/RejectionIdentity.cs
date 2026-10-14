using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Overrides;

/// <summary>
/// Identifies a rejected proposal without versions (spec §6.1): its kind, its key and its evidence's canonical keys and
/// span hashes. An edit elsewhere in a cited record keeps it; an edit to the cited span changes it and re-proposes the node.
/// </summary>
public static class RejectionIdentity
{
    /// <summary>The identity of a proposed node.</summary>
    /// <param name="node">The node.</param>
    /// <returns>64 lower-case hex digits.</returns>
    public static string For(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var evidence = node.EvidenceOrEmpty.Select(e => $"{e.Key.Value}#{e.Span}").Order(StringComparer.Ordinal);
        var text = $"{node.Key.Kind}\n{node.Key.Value}\n{string.Join('\n', evidence)}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
