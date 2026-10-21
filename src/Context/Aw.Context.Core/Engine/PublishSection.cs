using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.publish': how long a refreshed map waits for the code index of its commit.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record PublishSection(int? WaitForIndexMinutes = null);
