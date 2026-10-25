namespace Aw.Sindri.Ui.Platform;

/// <summary>Port: lets the human choose a file for a start input (the Start form). The app supplies the Avalonia one; an adapter answers null for no choice and never throws.</summary>
public interface IFilePicker
{
    /// <summary>Asks for one file.</summary>
    /// <param name="title">The picker's title.</param>
    /// <param name="limit">The most bytes the caller can use; a larger file is reported by its size and its content is left unread.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The file, or null when none was chosen or the picker is not there.</returns>
    public Task<PickedFile?> Pick(string title, int limit, CancellationToken cancellationToken);
}
