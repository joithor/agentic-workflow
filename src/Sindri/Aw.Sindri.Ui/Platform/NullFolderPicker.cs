namespace Aw.Sindri.Ui.Platform;

/// <summary>The chooser for a head with none: no folder is ever chosen, and the typed path is the only way.</summary>
internal sealed class NullFolderPicker : IFolderPicker
{
    /// <summary>The one instance.</summary>
    internal static NullFolderPicker Instance { get; } = new();

    public bool IsAvailable => false;

    public Task<string?> Pick(string title, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
}
