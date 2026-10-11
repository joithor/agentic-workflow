using System.Globalization;
using Aw.Kernel.Ids;

namespace Aw.Evidence.Testing;

/// <summary>A deterministic IIdGenerator: 00000000-0000-0000-0000-000000000001, …02, … in order.</summary>
public sealed class SequentialIds : IIdGenerator
{
    private int _next;

    /// <inheritdoc/>
    public Guid NewId() => Guid.Parse(string.Create(CultureInfo.InvariantCulture, $"00000000-0000-0000-0000-{Interlocked.Increment(ref _next):D12}"), CultureInfo.InvariantCulture);
}
