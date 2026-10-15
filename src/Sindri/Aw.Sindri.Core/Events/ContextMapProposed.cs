namespace Aw.Sindri.Core.Events;

/// <summary>A scoping run's surfaces were written to the latest draft (context spec §3 'context.map.proposed'; added by S-4).</summary>
/// <param name="Run">The scoping run's id.</param>
/// <param name="Repo">The repo.</param>
/// <param name="Draft">The draft's version number.</param>
/// <param name="Added">New proposals written.</param>
/// <param name="Existing">Surfaces the map has already.</param>
/// <param name="Rejected">Surfaces a person rejected earlier, so not proposed again.</param>
public sealed record ContextMapProposed(string Run, string Repo, int Draft, int Added, int Existing, int Rejected);
