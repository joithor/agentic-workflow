using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Results;

/// <summary>
/// What one source's sync did (spec §8.8, S-5B): counts and a code only, never record text (spec §7 "Logs and spans").
/// </summary>
/// <param name="Source">The source.</param>
/// <param name="Records">Records the source holds links for after this run.</param>
/// <param name="Fetched">Records fetched this run (new or changed versions).</param>
/// <param name="Unchanged">Hits whose stored version was current, so they weren't fetched again.</param>
/// <param name="Linked">Links written this run.</param>
/// <param name="Unlinked">Records fetched that no rule linked to a node.</param>
/// <param name="Orphaned">Stored links whose target no longer resolves in the map.</param>
/// <param name="Decisions">Decisions extracted this run.</param>
/// <param name="Queued">Records waiting for decision extraction (labeling off or over its cap).</param>
/// <param name="Refused">Model answers the engine refused (SND-CONTEXT-057).</param>
/// <param name="Tokens">Model tokens spent this run.</param>
/// <param name="Code">The code the run ended with: null when it completed, SND-CONTEXT-010 when the source was down, SND-CONTEXT-059 at its time cap, SND-CONTEXT-042 when extraction was held back.</param>
public sealed record SourceSync(SourceId Source, int Records, int Fetched, int Unchanged, int Linked, int Unlinked, int Orphaned, int Decisions, int Queued, int Refused, int Tokens, string? Code);
