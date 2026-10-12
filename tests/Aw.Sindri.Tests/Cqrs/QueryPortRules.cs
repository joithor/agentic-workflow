using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Tests.Cqrs;

/// <summary>ARCH-CQRS-002 (API spec §2.5): no query handler's constructor takes a write port (ILedgerStore, ILease, IApprovals, IViewStore).</summary>
internal static class QueryPortRules
{
    public const string Rule = "ARCH-CQRS-002";

    /// <summary>The ports a query handler may not take: the ledger and lease writers, the approvals writer and the projection writer (IViewStore).</summary>
    public static readonly IReadOnlyList<Type> WritePorts = [typeof(ILedgerStore), typeof(ILease), typeof(IApprovals), typeof(IViewStore)];

    /// <summary>S1's two queries read the ledger and approvals until P2's read-only ports land (P1 plan G1). Removed in P2 (C9).</summary>
    public static readonly IReadOnlySet<string> Allowlist = new HashSet<string>(StringComparer.Ordinal)
    {
        "Aw.Sindri.Features.GetStatus.GetStatusService",
        "Aw.Sindri.Features.StreamEvents.StreamEventsService",
    };

    /// <summary>The query handlers among <paramref name="types"/> that take a write port, outside <paramref name="allowlist"/>.</summary>
    public static IReadOnlyList<string> Check(IEnumerable<Type> types, IReadOnlySet<string> allowlist)
    {
        ArgumentNullException.ThrowIfNull(allowlist);
        return [.. Offenders(types).Where(o => !allowlist.Contains(o.Handler.FullName ?? o.Handler.Name))
            .Select(o => $"{Rule}: {o.Handler.Name} handles a query but takes {o.Port.Name}; read through a read-only port")];
    }

    /// <summary>Every query handler among <paramref name="types"/> with the write port it takes.</summary>
    public static IEnumerable<(Type Handler, Type Port)> Offenders(IEnumerable<Type> types) =>
        types.Where(IsQueryHandler).SelectMany(handler => handler.GetConstructors()
            .SelectMany(c => c.GetParameters()).Select(p => p.ParameterType).Where(WritePorts.Contains).Select(port => (handler, port)));

    private static bool IsQueryHandler(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
        && type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IService<,>)
            && i.GenericTypeArguments[0].GetInterfaces().Any(m => m.IsGenericType && m.GetGenericTypeDefinition() == typeof(IQuery<>)));
}
