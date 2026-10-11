namespace Aw.Evidence.Infrastructure.Images;

/// <summary>Bytes the PNG reader can't decode; PngCodec turns it into AW-EVIDENCE-012 and it never leaves the adapter.</summary>
internal sealed class InvalidPngException : Exception
{
    public InvalidPngException()
    {
    }

    public InvalidPngException(string message)
        : base(message)
    {
    }

    public InvalidPngException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
