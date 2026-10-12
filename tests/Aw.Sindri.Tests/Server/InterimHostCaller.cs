using Aw.Kernel.Ids;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Tests.Server;

/// <summary>Interim (see <see cref="InterimCallers"/>): reads as 'service:host' when no caller is current, as ServeCommand's own calls will be.</summary>
/// <param name="inner">The real accessor.</param>
/// <param name="ids">Ids for the host caller.</param>
internal sealed class InterimHostCaller(ICallerContextAccessor inner, IIdGenerator ids) : ICallerContextAccessor
{
    public CallerContext? Current => inner.Current ?? Host;

    private CallerContext Host { get; } = CallerContext.Service("host", ids);

    public IDisposable Enter(CallerContext context) => inner.Enter(context);

    public IDisposable EnterService(string service) => inner.EnterService(service);
}
