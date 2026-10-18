using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The unix socket table as a dictionary.</summary>
internal sealed class FakeUnixSockets(IReadOnlyDictionary<long, string>? sockets = null) : IUnixSockets
{
    public IReadOnlyDictionary<long, string> Read() => sockets ?? new Dictionary<long, string>();
}
