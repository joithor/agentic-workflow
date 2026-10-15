using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Tests.Contract;

/// <summary>Holds one of each Context id, so the JSON tests can send a missing, null or malformed property.</summary>
public sealed record IdsHolder(SourceId Source, ExternalId External, SourceVersion Version, RecordKey Key, SourceCursor Cursor, QueryId Query, ContextRepoName Repo);
