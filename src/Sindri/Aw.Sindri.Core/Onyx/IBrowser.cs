namespace Aw.Sindri.Core.Onyx;

/// <summary>Opens a page in the human's browser.</summary>
public interface IBrowser
{
    /// <summary>Opens a loopback page; when there is no way to open one, the caller prints the address instead.</summary>
    /// <param name="url">The page.</param>
    /// <returns>True when a browser was asked to open it.</returns>
    public bool Open(Uri url);
}
