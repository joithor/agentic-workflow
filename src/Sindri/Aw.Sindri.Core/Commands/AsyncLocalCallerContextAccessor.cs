using Aw.Kernel.Ids;

namespace Aw.Sindri.Core.Commands;

/// <summary>The caller held in an AsyncLocal: set for a call, it flows into every await below it and is restored on dispose.</summary>
/// <param name="ids">The id port, for a service caller's ids.</param>
internal sealed class AsyncLocalCallerContextAccessor(IIdGenerator ids) : ICallerContextAccessor
{
    private readonly AsyncLocal<CallerContext?> _current = new();

    public CallerContext? Current => _current.Value;

    public IDisposable Enter(CallerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = _current.Value;
        _current.Value = context;
        return new Restore(_current, previous);
    }

    public IDisposable EnterService(string service) => Enter(CallerContext.Service(service, ids));

    private sealed class Restore(AsyncLocal<CallerContext?> current, CallerContext? previous) : IDisposable
    {
        public void Dispose() => current.Value = previous;
    }
}
