namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What the engine does with a <see cref="Launch.FailedLaunch"/> (engine P8 D8).</summary>
/// <param name="Kind">Park or fail the Step.</param>
/// <param name="Code">The error code the launcher gave.</param>
/// <param name="Message">The launcher's message, which the park's question or the failure's reason carries.</param>
public sealed record AgentLaunchFailure(AgentLaunchFailureKind Kind, string Code, string Message)
{
    /// <summary>The text of the park's question or of the 'step.failed' reason.</summary>
    public string Text => Kind == AgentLaunchFailureKind.Park ? $"{Code}: {Message}" : $"launch: {Code}: {Message}";
}
