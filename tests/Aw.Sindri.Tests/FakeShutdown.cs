using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Tests;

/// <summary>The shutdown port as a flag and a token.</summary>
internal sealed class FakeShutdown : IShutdown, IDisposable
{
    private readonly CancellationTokenSource _stopping = new();

    public bool Requested => _stopping.IsCancellationRequested;

    public CancellationToken Stopping => _stopping.Token;

    public void Request() => _stopping.Cancel();

    public void Dispose() => _stopping.Dispose();
}
