namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>What the owner did at stop 3 (the ready-to-merge state), by the transition each action took out of it.</summary>
/// <param name="Merge">Merges.</param>
/// <param name="Rework">Rework sends.</param>
/// <param name="Comments">Held-comment walks.</param>
/// <param name="ResyncDocs">Docs resyncs.</param>
/// <param name="Check">Checks.</param>
public sealed record OwnerActionStats(int Merge, int Rework, int Comments, int ResyncDocs, int Check);
