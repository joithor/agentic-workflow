namespace Aw.Sindri.Ui.Platform;

/// <summary>A file the platform's chooser returned, before its bytes are read.</summary>
/// <param name="Name">The file's name, without its folder.</param>
/// <param name="Size">Its size in bytes when the platform says, else null.</param>
/// <param name="Open">Opens its content for reading.</param>
/// <param name="Owner">What holds the file open for the platform (a storage item), released once it has been read; or null.</param>
internal sealed record ChosenFile(string Name, long? Size, Func<CancellationToken, Task<Stream>> Open, IDisposable? Owner = null);
