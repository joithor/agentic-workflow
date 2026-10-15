namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>One declared input of the instance's recipe (E-20).</summary>
/// <param name="Key">The input key.</param>
/// <param name="Label">The label.</param>
/// <param name="Kind">The input's kind.</param>
/// <param name="Required">Whether the recipe requires it.</param>
/// <param name="Present">Whether the instance has it.</param>
public sealed record InputSlot(
    string Key,
    string Label,
    string Kind,
    bool Required,
    bool Present);
