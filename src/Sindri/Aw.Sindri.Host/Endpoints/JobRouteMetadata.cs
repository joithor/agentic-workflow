namespace Aw.Sindri.Host.Endpoints;

/// <summary>Marks a route MapJob maps with a request delegate (its handler takes no typed IService parameter), naming the job request its chained start service takes, so ARCH-CQRS-001 can see it.</summary>
/// <param name="Request">The job request type.</param>
internal sealed record JobRouteMetadata(Type Request);
