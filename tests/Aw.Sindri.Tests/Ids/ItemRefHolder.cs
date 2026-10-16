using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Ids;

/// <summary>Wraps an ItemRef so the JSON tests can read and write it as a property.</summary>
public sealed record ItemRefHolder(ItemRef Id);
