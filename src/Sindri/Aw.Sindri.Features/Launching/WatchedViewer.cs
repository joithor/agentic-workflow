using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.Launching;

/// <summary>A started launch whose viewer reported 'unconfirmed': polled until a client attaches or <paramref name="Until"/> passes (UI spec E-1, launcher L-10).</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Host">Its session.</param>
/// <param name="Until">When to stop polling.</param>
internal sealed record WatchedViewer(LaunchId Launch, HostRef Host, DateTimeOffset Until);
