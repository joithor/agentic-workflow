using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IMessageStore: the one fake, kept honest by MessageStoreContract.</summary>
public sealed class InMemoryMessageStore : IMessageStore;
