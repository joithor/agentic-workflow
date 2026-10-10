using Aw.Architecture.Tests.Fixtures.Good.Host.Endpoints;

// A global-namespace type (like a top-level Program's state machine) that holds a Host type: with the endpoint's
// namespace-less Cast<T> target, the cycle graph must not join the global namespace and Host.Endpoints into a cycle.
#pragma warning disable CA1050, MA0047 // Justification: the fixture exists to be in the global namespace.
internal static class GlobalNamespaceProbe
#pragma warning restore CA1050, MA0047
{
    public static StartItemBody Body => new("probe");
}
