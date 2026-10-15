namespace Aw.Sindri.Core.Events;

/// <summary>The map was proposed from the repo into a draft (context spec §3 'context.map.built'; the context-rebuild service, and the build job after API P6).</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Draft">The draft's version number.</param>
/// <param name="Proposed">Nodes proposed.</param>
/// <param name="Degraded">What the build did without ('clusters', 'labels'); empty when nothing.</param>
public sealed record ContextMapBuilt(string Repo, int Draft, int Proposed, IReadOnlyList<string> Degraded);
