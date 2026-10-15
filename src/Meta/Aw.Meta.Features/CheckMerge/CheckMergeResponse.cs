using Aw.Meta.Core.Merge;


namespace Aw.Meta.Features.CheckMerge;

/// <summary>The CheckMerge response.</summary>
/// <param name="Pr">The pull request number.</param>
/// <param name="Ready">Whether every check holds.</param>
/// <param name="Checks">Each check with its detail.</param>
public sealed record CheckMergeResponse(int Pr, bool Ready, IReadOnlyList<MergeCheck> Checks);
