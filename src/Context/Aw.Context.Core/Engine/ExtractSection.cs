using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.extract': the profile's extra surface recognizers (S-SK, scoping station C2).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ExtractSection(IReadOnlyList<RecognizerEntry?>? Recognizers = null, GraphifySection? Graphify = null);
