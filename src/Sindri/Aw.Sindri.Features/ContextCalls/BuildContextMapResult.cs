namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What a build job did, small by design (the job's result is capped at 16 KiB): the draft, how many nodes were proposed and what degraded.</summary>
/// <param name="Repo">The repo the draft is for.</param>
/// <param name="Draft">The draft's version number.</param>
/// <param name="Proposed">How many nodes the build proposed.</param>
/// <param name="Degraded">What degraded, at most 20 lines (no labeling, a cluster pass that failed).</param>
public sealed record BuildContextMapResult(string Repo, int Draft, int Proposed, IReadOnlyList<string> Degraded);
