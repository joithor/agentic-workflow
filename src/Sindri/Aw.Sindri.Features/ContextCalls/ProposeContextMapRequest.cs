using Aw.Context.Core.Proposals;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// A scoping run proposes surfaces for the map (context spec §8.5, README X63): 'propose' rows in the latest draft (opened as a
/// copy of the current map when there is none), never the current map. A job-class command that runs synchronously: it only
/// writes draft rows. Terminal, the app and the engine's own services may call it; a session may not.
/// </summary>
/// <param name="Run">The scoping run's id.</param>
/// <param name="Repo">The repo the surfaces are about.</param>
/// <param name="Surfaces">The surfaces: a name, and a key or a kind, with evidence and a brief quote.</param>
public sealed record ProposeContextMapRequest(string Run, string Repo, IReadOnlyList<ProposedSurface> Surfaces) : ICommand<ProposeContextMapResponse>;
