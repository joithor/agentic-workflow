using Aw.Context.Core.Engine;

namespace Aw.Context.Testing.Engine;

/// <summary>An <see cref="ICurrentUser"/> with a fixed name; <see cref="CurrentUserContract"/> holds it to the OS user adapter's behavior.</summary>
/// <param name="Name">The user's name.</param>
public sealed record FakeCurrentUser(string Name = "avery") : ICurrentUser;
