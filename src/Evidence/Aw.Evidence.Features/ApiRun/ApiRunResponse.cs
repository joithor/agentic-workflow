using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>What an api-run produced: the summary as written and the exact JSON text of the file (what stdout carries).</summary>
/// <param name="Summary">The summary, read back from <paramref name="Document"/>.</param>
/// <param name="Document">The scrubbed summary JSON, byte for byte what was written to summary.json.</param>
public sealed record ApiRunResponse(ApiSummary Summary, string Document);
