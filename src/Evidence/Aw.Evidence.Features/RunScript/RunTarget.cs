using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Features.RunScript;

/// <summary>Where a run drives and records: the vetted host, the run directory, and whether writes are allowed there.</summary>
/// <param name="Host">The vetted host.</param>
/// <param name="RunDirectory">The run directory (absolute).</param>
/// <param name="AllowWrites">Whether --allow-writes was given (a preview host refuses write-looking repairs without it).</param>
internal sealed record RunTarget(ResolvedHost Host, string RunDirectory, bool AllowWrites);
