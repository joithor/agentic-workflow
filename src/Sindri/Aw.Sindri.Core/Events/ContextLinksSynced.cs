namespace Aw.Sindri.Core.Events;

/// <summary>
/// What one intent-source sync did, counts and a code only (context spec §7 "Logs and spans": never record text). Rides on
/// <see cref="ContextSourcesSynced"/> beside the repo rows of a docs refresh.
/// </summary>
/// <param name="Records">Records stored for the source after the run.</param>
/// <param name="Fetched">Records fetched this run.</param>
/// <param name="Unchanged">Hits whose stored version was current.</param>
/// <param name="Linked">Links written this run.</param>
/// <param name="Unlinked">Records fetched that no rule linked.</param>
/// <param name="Orphaned">Stored links whose target no longer resolves.</param>
/// <param name="Decisions">Decisions stored for the source.</param>
/// <param name="Queued">Records waiting for decision extraction.</param>
/// <param name="Refused">Model answers the engine refused.</param>
/// <param name="Tokens">Model tokens spent this run.</param>
/// <param name="Code">The code the run ended with, or null.</param>
public sealed record ContextLinksSynced(int Records, int Fetched, int Unchanged, int Linked, int Unlinked, int Orphaned, int Decisions, int Queued, int Refused, int Tokens, string? Code);
