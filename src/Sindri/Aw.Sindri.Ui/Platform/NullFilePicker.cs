namespace Aw.Sindri.Ui.Platform;

/// <summary>The picker for a head with none: no file is ever chosen.</summary>
internal sealed class NullFilePicker : IFilePicker
{
    /// <summary>The one instance.</summary>
    internal static NullFilePicker Instance { get; } = new();

    public Task<PickedFile?> Pick(string title, int limit, CancellationToken cancellationToken) => Task.FromResult<PickedFile?>(null);
}
