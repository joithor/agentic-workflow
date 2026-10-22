using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>GET /sindri/context/map/publish: whether the current map is held for the code index, and how it was published (code index spec 6.4), cut to the caller's repos.</summary>
internal sealed record GetMapPublishStatusRequest : IQuery<MapPublishStatusView>;
