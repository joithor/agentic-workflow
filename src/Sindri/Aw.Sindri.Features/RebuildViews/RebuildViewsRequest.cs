using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.RebuildViews;

/// <summary>Rebuild the views from the ledger (POST /sindri/views/rebuild, 'sindri views rebuild'; API spec §3.2): desk class, never waits for the head.</summary>
public sealed record RebuildViewsRequest() : ICommand<CommandAccepted>;
