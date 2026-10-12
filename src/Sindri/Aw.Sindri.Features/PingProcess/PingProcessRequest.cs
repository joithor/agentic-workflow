using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Features.PingProcess;

/// <summary>Is this the control plane, and at which epoch? (Clients check a stale endpoint record with it.)</summary>
public sealed record PingProcessRequest : IQuery<Ping>;
