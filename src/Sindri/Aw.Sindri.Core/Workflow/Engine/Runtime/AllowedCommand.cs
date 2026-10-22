using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// One command the instance accepts now (engine spec rev 8 §5.1, §6.3; UI spec E-13 '{ actionId, cli }'): the API
/// catalog action the app calls ('CompleteStep', 'ApplyCommand:cancel') and the CLI line a human runs. A recipe verb
/// ('command.&lt;verb&gt;' on a transition out of the current state) is one entry with the generic 'ApplyCommand:command' action and its
/// <see cref="Verb"/>, so the app can show one button per verb (PR babysitter spec API18).
/// </summary>
/// <param name="Action">The catalog action, written as 'actionId'.</param>
/// <param name="Cli">The 'sindri workflow …' line.</param>
public sealed record AllowedCommand([property: JsonPropertyName("actionId")] string Action, string Cli)
{
    /// <summary>The recipe verb of an 'ApplyCommand:command' entry, or null for every other command (omitted from the JSON then).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Verb { get; init; }
}
