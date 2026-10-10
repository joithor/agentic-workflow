using Aw.Kernel.Ids;

namespace Aw.Kernel.AotSmoke;

internal sealed class SmokeIds : IIdGenerator
{
    private int _next;

    public Guid NewId() => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)++_next);
}
