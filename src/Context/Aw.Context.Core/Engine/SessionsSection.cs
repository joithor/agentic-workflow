using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.sessions': whether agent sessions may read context.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SessionsSection(bool Enabled = false);
