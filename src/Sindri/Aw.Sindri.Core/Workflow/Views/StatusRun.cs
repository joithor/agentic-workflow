using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The open Step run of an agent session, as the status shows it (P8).</summary>
/// <param name="Runtime">Who runs the Step: 'agent'.</param>
/// <param name="Launch">The launch request ('LCH-…') that started the session; the handle the launcher knows it by.</param>
/// <param name="Session">The session's provider and id once the launch knows them; null (left out of the JSON) before. Kept while the instance is parked or taken over.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusRun(
    string Runtime,
    string? Launch,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SessionHandle? Session = null);
