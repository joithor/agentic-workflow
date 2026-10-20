namespace Aw.Sindri.Core.Line;

/// <summary>Reads a line packet's index.json (line spec §3.2): kind, parents and the acceptance chain.</summary>
public static class LinePacketIndex
{
    /// <summary>The parent hashes an index.json lists.</summary>
    /// <param name="indexJson">The text of index.json.</param>
    /// <returns>The parents in order; none when the text is no JSON object or lists no parents.</returns>
    public static IReadOnlyList<string> Parents(string indexJson) => throw new NotSupportedException();
}
