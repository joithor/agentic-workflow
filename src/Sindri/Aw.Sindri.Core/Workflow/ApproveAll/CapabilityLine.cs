namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>One capability of a recipe in plain words (setup-once spec §5.3.1): a block descriptor id, an effect kind or a capability literal, and the sentence for it.</summary>
/// <param name="Id">The machine id, shown in a small secondary line.</param>
/// <param name="Sentence">The plain sentence shown first.</param>
public sealed record CapabilityLine(CapabilityId Id, string Sentence);
