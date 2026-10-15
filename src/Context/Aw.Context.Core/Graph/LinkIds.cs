using System.Security.Cryptography;
using System.Text;

namespace Aw.Context.Core.Graph;

/// <summary>Gives an edge its <see cref="LinkId"/>.</summary>
public static class LinkIds
{
    /// <summary>The id of <paramref name="edge"/>: 'edge:' and the first 16 hex digits of the SHA-256 of 'from', 'to' and the type's name, one per line.</summary>
    /// <param name="edge">The edge.</param>
    /// <returns>Its id; the same fact always gets the same one.</returns>
    public static LinkId For(GraphEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        var type = edge.Type.ToString().ToLowerInvariant();
        var text = $"{edge.From.Value}\n{edge.To.Value}\n{type}";
        return LinkId.From("edge:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16]);
    }
}
