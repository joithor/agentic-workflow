namespace Aw.Sindri.Ui.Platform;

/// <summary>Port: lets the human choose a folder on this computer (the repo step's Browse). The app supplies the Avalonia one on a desktop head; an adapter answers null for no choice and never throws.</summary>
public interface IFolderPicker
{
    /// <summary>True when this head can show a folder chooser; a head with none shows the typed path only.</summary>
    public bool IsAvailable { get; }

    /// <summary>Asks for one folder.</summary>
    /// <param name="title">The chooser's title.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The folder's absolute local path, or null when none was chosen or the chooser is not there.</returns>
    public Task<string?> Pick(string title, CancellationToken cancellationToken);
}
