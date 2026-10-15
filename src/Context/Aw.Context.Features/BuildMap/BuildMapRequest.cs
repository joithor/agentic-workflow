using Aw.Context.Core.Engine;

namespace Aw.Context.Features.BuildMap;

/// <summary>Proposes the map from the repo and writes a draft (heavy-job lock, extraction, clusters, optional labeling).</summary>
internal sealed record BuildMapRequest(IProgress<string>? Progress, ContextScope Scope);
