namespace Aw.Sindri.Ui.Api;

/// <summary>POST /sindri/profile/approve's answer: the hash that was approved and the key paths that changed since the last approval.</summary>
/// <param name="Hash">The approved profile's hash.</param>
/// <param name="Changes">The key paths that differ from the previous approval.</param>
/// <param name="Approved">True once the profile is approved (false for a preview).</param>
/// <param name="Sequence">The ledger sequence of the approval; null for a preview.</param>
internal sealed record ProfileApproved(string Hash, IReadOnlyList<string> Changes, bool Approved, long? Sequence);
