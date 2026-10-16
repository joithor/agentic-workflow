namespace Aw.Sindri.Features.Jobs;

/// <summary>A DI scope for one job's work (API P6 unit C), opened by the registration (only a registration lambda may touch the container): what resolves the work, and what ends the scope.</summary>
/// <param name="Resolve">Resolves a service from the scope.</param>
/// <param name="Lifetime">Disposes the scope.</param>
internal sealed record JobScope(Func<Type, object> Resolve, IAsyncDisposable Lifetime);
