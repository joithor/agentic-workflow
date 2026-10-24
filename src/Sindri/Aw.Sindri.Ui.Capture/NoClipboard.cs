using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Capture;

/// <summary>A clipboard that takes nothing: a capture never copies.</summary>
internal sealed class NoClipboard : IClipboard
{
    public Task<bool> SetText(string text, CancellationToken cancellationToken) => Task.FromResult(false);
}
