namespace Aw.Sindri.Host;

/// <summary>The part of an API ProblemDetails answer the CLI reads: the SND code (title) and the message (detail).</summary>
/// <param name="Title">The error code.</param>
/// <param name="Detail">The message.</param>
/// <param name="Status">The HTTP status.</param>
/// <param name="RunningJob">The job already running, on an SND-JOB-409 for a start (the 'runningJob' extension), else null.</param>
/// <param name="Building">The 'building' extension of the whole map's "no map yet" answer while a map build runs, else null.</param>
/// <param name="NoMap">The 'noMap' extension of the whole map's "no map yet" answer, else null.</param>
internal sealed record ApiProblem(string? Title, string? Detail, int? Status, string? RunningJob = null, ApiProblemBuilding? Building = null, ApiProblemNoMap? NoMap = null);
