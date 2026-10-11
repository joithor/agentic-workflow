using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Tests.Ids;

/// <summary>Wraps a RunInstanceId so the JSON tests can send a missing or null property.</summary>
public sealed record RunInstanceIdHolder(RunInstanceId Id);
