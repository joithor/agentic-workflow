using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Testing;

/// <summary>A transport arranged by an <see cref="ApiTransportContract"/> subclass: the transport, the origin its calls go to and what the server received.</summary>
/// <param name="Transport">The transport under test.</param>
/// <param name="Origin">The origin the calls go to.</param>
/// <param name="Received">The requests the server received, each as 'METHOD path body X-Probe=value' text.</param>
public sealed record ApiUnderTest(IApiTransport Transport, string Origin, Func<IReadOnlyList<string>> Received);
