using System.Buffers.Binary;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Images;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>An IImageCodec over a trivial format ('RGBA', width and height as big-endian int32, then the pixels), so unit tests need no PNG.</summary>
public sealed class FakeImageCodec : IImageCodec
{
    private static readonly byte[] _magic = "RGBA"u8.ToArray();

    /// <inheritdoc/>
    public Result<RgbaImage> Decode(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length < 12 || !encoded[..4].SequenceEqual(_magic))
        {
            return EvidenceErrors.ImageInvalid.With("not a fake RGBA image");
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(encoded[4..]);
        var height = BinaryPrimitives.ReadInt32BigEndian(encoded[8..]);
        return RgbaImage.Create(width, height, encoded[12..]);
    }

    /// <inheritdoc/>
    public byte[] Encode(RgbaImage image)
    {
        var bytes = new byte[12 + image.Pixels.Length];
        _magic.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(4), image.Width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), image.Height);
        image.Pixels.CopyTo(bytes.AsSpan(12));
        return bytes;
    }
}
