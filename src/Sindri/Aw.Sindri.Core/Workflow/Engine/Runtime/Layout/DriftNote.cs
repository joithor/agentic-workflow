namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>A drift recorded behind a park (E-23).</summary>
/// <param name="Block">The block that changed.</param>
/// <param name="NeedsReapproval">Whether the answer needs the re-approval first.</param>
public sealed record DriftNote(
    string Block,
    bool NeedsReapproval);
