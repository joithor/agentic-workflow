namespace Aw.BugFix.Core.Ports;

/// <summary>The ports the work-tree check reads: git for the tree, the file system for hashes, the ledger for the runs.</summary>
/// <param name="Git">The git port.</param>
/// <param name="Files">The file-system port.</param>
/// <param name="Ledger">The run ledger.</param>
public sealed record TreePorts(IGit Git, IFiles Files, IRunLedger Ledger);
