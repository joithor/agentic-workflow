using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Tests.ListInstances;

/// <summary>A caller accessor that always reports the same caller (or none).</summary>
internal sealed class FixedCaller(CallerContext? current) : ICallerContextAccessor
{
    public CallerContext? Current { get; } = current;

    public IDisposable Enter(CallerContext context) => throw new NotSupportedException();

    public IDisposable EnterService(string service) => throw new NotSupportedException();
}
