using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Tests;

/// <summary>The shutdown port as a flag.</summary>
internal sealed class FakeShutdown : IShutdown
{
    public bool Requested { get; private set; }

    public void Request() => Requested = true;
}
