namespace Aw.Evidence.Infrastructure.Images;

/// <summary>The chunks a decode needs: the header, the palette and transparency (when present) and the joined IDAT data.</summary>
/// <param name="Header">IHDR.</param>
/// <param name="Palette">PLTE's RGB triples, or empty.</param>
/// <param name="Transparency">tRNS's data, or empty.</param>
/// <param name="Compressed">Every IDAT chunk's data, in order.</param>
internal sealed record PngParts(PngHeader Header, byte[] Palette, byte[] Transparency, byte[] Compressed);
