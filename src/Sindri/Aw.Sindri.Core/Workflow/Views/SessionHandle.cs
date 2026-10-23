using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The agent session behind a Step run, addressed the way the Agents page addresses it (q65): the app opens session/{provider}/{id}.</summary>
/// <param name="Provider">The agent ('claude-code').</param>
/// <param name="Id">The provider's session id.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SessionHandle(string Provider, string Id);
