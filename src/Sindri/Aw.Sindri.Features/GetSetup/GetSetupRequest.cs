using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetSetup;

/// <summary>Where "set up Sindri once" stands (GET /sindri/setup): a pure read that starts nothing.</summary>
public sealed record GetSetupRequest() : IQuery<GetSetupResponse>;
