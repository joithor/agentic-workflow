using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory clipboard: what was copied last, or a refusal when the test says so.</summary>
internal sealed class FakeClipboard : IClipboard
{
    /// <summary>The text copied last, or null.</summary>
    public string? Text { get; private set; }

    /// <summary>When true, every write is refused.</summary>
    public bool Refuse { get; set; }

    public Task<bool> SetText(string text, CancellationToken cancellationToken)
    {
        if (Refuse)
        {
            return Task.FromResult(false);
        }

        Text = text;
        return Task.FromResult(true);
    }
}
