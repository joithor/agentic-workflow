namespace Aw.Evidence.Core.Images;

/// <summary>Two same-size RGBA buffers read together: the image a pixel is judged in, and the other one.</summary>
internal readonly ref struct ImagePair(ReadOnlySpan<byte> image, ReadOnlySpan<byte> other, int width, int height)
{
    public ReadOnlySpan<byte> Image { get; } = image;

    public ReadOnlySpan<byte> Other { get; } = other;

    public int Width { get; } = width;

    public int Height { get; } = height;

    public ImagePair Swapped => new(Other, Image, Width, Height);

    public int Offset(int x, int y) => ((y * Width) + x) * 4;

    public bool OnEdge(int x, int y) => x == 0 || y == 0 || x == Width - 1 || y == Height - 1;
}
