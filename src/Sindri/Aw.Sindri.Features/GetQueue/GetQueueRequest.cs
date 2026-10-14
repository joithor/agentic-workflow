using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetQueue;

/// <summary>The queue: open items in the fixed groups (GET /sindri/queue; API spec §3.1, §3.6).</summary>
public sealed record GetQueueRequest() : IQuery<QueueResponse>;
