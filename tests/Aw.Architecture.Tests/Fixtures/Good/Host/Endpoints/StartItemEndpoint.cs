using Aw.Architecture.Tests.Fixtures.Good.Features.StartItem;

namespace Aw.Architecture.Tests.Fixtures.Good.Host.Endpoints;

// The request-delegate generator emits a file-local generic Cast<T>(Delegate, T) that each mapped endpoint calls;
// ArchUnitNET records its T as a dependency target with no namespace. This models that shape.
internal static class StartItemEndpoint
{
    public static Func<StartItemBody, StartItemService, string?> Map()
    {
        Func<StartItemBody, StartItemService, string?> handler = static (body, service) => service.Start(body.Name)?.Name;
        return GeneratedCast.Cast(handler, handler);
    }
}

file static class GeneratedCast
{
    public static T Cast<T>(Delegate handler, T fallback) => handler is T typed ? typed : fallback;
}
