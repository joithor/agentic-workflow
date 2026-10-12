using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>A clipboard that refuses every write: for a view model built without the window's clipboard (tests that don't copy).</summary>
internal sealed class NoClipboard : IClipboard
{
    /// <summary>The one instance.</summary>
    internal static NoClipboard Instance { get; } = new();

    public Task<bool> SetText(string text, CancellationToken cancellationToken) => Task.FromResult(false);
}
