namespace Aw.Packets.Core.Kinds.Line;

/// <summary>The 'links' object of a line kind's packet.json: one entry per file the kind lists, and one list per linked directory.</summary>
public interface ILineLinks
{
    /// <summary>Each link key with the paths it names (one path for a file; the files of a linked directory).</summary>
    /// <returns>The entries by key.</returns>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Entries();
}
