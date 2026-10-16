namespace Aw.Context.Core.Linking;

/// <summary>What committing a page did.</summary>
/// <param name="Records">Records in the page.</param>
/// <param name="Links">Links stored for them.</param>
/// <param name="Changed">Records that were new or whose version moved (queued for extraction); a replay of the same page reports 0.</param>
public sealed record IntentCommit(int Records, int Links, int Changed);
