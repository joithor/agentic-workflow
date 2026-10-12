using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory human token: whatever text the test put there, or none (as before the first 'sindri serve').</summary>
internal sealed class InMemoryHumanTokens : IHumanTokenSource
{
    private Exception? _fail;

    /// <summary>Gets or sets the token text the source holds (null for none).</summary>
    public string? Text { get; set; }

    public string Location { get; set; } = "memory:auth/human.token";

    /// <summary>Makes the next read throw, as a token file the platform can't read does.</summary>
    /// <param name="error">The exception.</param>
    public void FailNextRead(Exception error) => _fail = error;

    public ValueTask<string?> Read(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _fail, value: null) is { } error)
        {
            throw error;
        }

        return ValueTask.FromResult(Text?.Trim() is { Length: > 0 } text ? text : null);
    }
}
