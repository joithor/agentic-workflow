using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Tests.Ids;

/// <summary>Wraps a TestId so the JSON tests can send a missing or null property.</summary>
public sealed record TestIdHolder(TestId Id);
