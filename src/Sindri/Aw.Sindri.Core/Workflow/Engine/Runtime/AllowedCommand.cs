using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// One command the instance accepts now (engine spec rev 8 §5.1, §6.3; UI spec E-13 '{ actionId, cli }'): the API
/// catalog action the app calls ('CompleteStep', 'ApplyCommand:cancel') and the CLI line a human runs.
/// </summary>
/// <param name="Action">The catalog action, written as 'actionId'.</param>
/// <param name="Cli">The 'sindri workflow …' line.</param>
public sealed record AllowedCommand([property: JsonPropertyName("actionId")] string Action, string Cli);
