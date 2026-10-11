using Aw.Kernel.Ids;

namespace Aw.Evidence.Infrastructure.Ids;

/// <summary>The real id source: a random (version 4) Guid per id.</summary>
internal sealed class RandomIds : IIdGenerator
{
    /// <inheritdoc/>
    public Guid NewId() => Guid.NewGuid();
}
