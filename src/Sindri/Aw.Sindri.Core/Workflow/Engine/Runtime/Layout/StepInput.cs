namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>One input of the Step (E-23).</summary>
/// <param name="Key">The input key.</param>
/// <param name="Name">The input's name.</param>
/// <param name="Hash">Its content hash, when known.</param>
public sealed record StepInput(
    string Key,
    string Name,
    string? Hash);
