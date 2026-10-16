using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Ids;

/// <summary>Wraps a JobId so the JSON tests can send a missing or null property.</summary>
public sealed record JobIdHolder(JobId Id);
