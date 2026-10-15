using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Ids;

/// <summary>Wraps a RecordKey so the JSON tests can send a missing or null property.</summary>
public sealed record RecordKeyHolder(RecordKey Id);
