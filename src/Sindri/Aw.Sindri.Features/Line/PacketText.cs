namespace Aw.Sindri.Features.Line;

/// <summary>One file of a packet a Step assembles: its name in the packet directory and its text.</summary>
/// <param name="Name">The file name, 'spec.md'.</param>
/// <param name="Content">The text, UTF-8 without a byte order mark.</param>
internal sealed record PacketText(string Name, string Content);
