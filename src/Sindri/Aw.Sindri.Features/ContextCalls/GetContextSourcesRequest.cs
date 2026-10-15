using Aw.Context.Core.Results;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The context sources' state (GET /sindri/context/sources, 'sindri context sources status').</summary>
public sealed record GetContextSourcesRequest() : IQuery<SourcesView>;
