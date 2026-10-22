using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.EngineReads;

namespace Aw.Sindri.Features.GetEngine;

/// <summary>'GET /sindri/engine' (API spec §3.8, E-30; query): the Temporal server, the worker, the mirror's lag and what the instances wait on.</summary>
public sealed record GetEngineRequest() : IQuery<EngineView>;
