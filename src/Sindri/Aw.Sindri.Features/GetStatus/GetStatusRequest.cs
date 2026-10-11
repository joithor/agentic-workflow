using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetStatus;

/// <summary>The GetStatus request.</summary>
public sealed record GetStatusRequest() : IQuery<GetStatusResponse>;
