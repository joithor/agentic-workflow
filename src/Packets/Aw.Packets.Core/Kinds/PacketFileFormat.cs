namespace Aw.Packets.Core.Kinds;

/// <summary>How a packet file's bytes are read.</summary>
public enum PacketFileFormat
{
    /// <summary>One JSON object (strict: no byte order mark, no duplicate keys, no null in a list).</summary>
    Json = 1,

    /// <summary>One JSON object per non-blank line.</summary>
    JsonLines = 2,

    /// <summary>UTF-8 text.</summary>
    Text = 3,
}
