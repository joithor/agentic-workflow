using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Proposes the map from the repo into a draft now (context spec §8.7, API spec §4.1): a job-class command that starts a job and answers its links.
/// The same work as the 'context-rebuild' scheduled service, on demand, with the engine's progress lines on the job and the same
/// 'context.map.built' event. A terminal, the app and the engine's own services may start it; a session may not.
/// </summary>
public sealed record BuildContextMapRequest : IJob<BuildContextMapResult>;
