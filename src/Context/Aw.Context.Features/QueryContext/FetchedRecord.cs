using Aw.Context.Core.Ranking;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;

namespace Aw.Context.Features.QueryContext;

/// <summary>A fused item with its record fetched and cleaned at the ingest boundary.</summary>
/// <param name="Item">The fused item.</param>
/// <param name="Source">The source it was fetched from (its best hit's).</param>
/// <param name="Title">The cleaned, one-line title.</param>
/// <param name="Text">The cleaned text, or a blocked field.</param>
/// <param name="Aliases">The item's aliases, each cleaned on its own as one line (record text, spec §7).</param>
internal sealed record FetchedRecord(FusedItem Item, IContextSource Source, ScrubbedField Title, ScrubbedField Text, IReadOnlyList<ScrubbedField> Aliases);
