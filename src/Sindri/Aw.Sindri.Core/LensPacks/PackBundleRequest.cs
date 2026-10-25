using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.LensPacks;

/// <summary>What an agent Step asks of <see cref="IPackBundles"/> (LN-WF5).</summary>
/// <param name="Bundle">The Step's 'bundle' parameter: 'style' or 'lenses'.</param>
/// <param name="Repo">The repo the instance works on, for the profile's per-repo pack; null for none.</param>
/// <param name="Subject">The Step's 'subject' parameter ('design' or 'change'), which says which lens loop's pack to copy; null for the design loop's.</param>
/// <param name="Lens">The queued lens whose prompt is copied; null to copy the pack's 'pack.json' alone.</param>
public sealed record PackBundleRequest(string Bundle, RepoName? Repo, string? Subject, string? Lens);
