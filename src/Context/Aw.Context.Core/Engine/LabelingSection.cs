using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.labeling': whether a build may name clusters with a model, and the most tokens one pass may spend.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record LabelingSection(bool? Enabled = null, int? MaxTokens = null, int? MaxTokensPerSync = null);
