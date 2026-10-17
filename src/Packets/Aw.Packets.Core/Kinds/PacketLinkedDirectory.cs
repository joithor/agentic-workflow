namespace Aw.Packets.Core.Kinds;

/// <summary>A sub-directory a packet kind allows, whose files packet.json lists one by one (any format; hashed by content).</summary>
/// <param name="Name">The directory name, without a trailing '/'.</param>
/// <param name="Link">The key under 'links' in packet.json that lists the files.</param>
public sealed record PacketLinkedDirectory(string Name, string Link)
{
    /// <summary>The prefix of every file path in the directory, 'name/'.</summary>
    public string Prefix => Name + "/";
}
