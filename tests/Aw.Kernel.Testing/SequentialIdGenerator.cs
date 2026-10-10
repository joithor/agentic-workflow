using Aw.Kernel.Ids;

namespace Aw.Kernel.Testing;

/// <summary>
/// A deterministic <see cref="IIdGenerator"/>: ids 1, 2, 3… within a scope, written as
/// '&lt;scope:8 hex&gt;-0000-0000-0000-&lt;n:12 hex&gt;', so a failing test prints readable ids.
/// </summary>
/// <param name="scope">Separates generators in one test, e.g. one per aggregate. 0 to 0xFFFFFFFF.</param>
public sealed class SequentialIdGenerator(uint scope = 0) : IIdGenerator
{
    private long _issued;

    /// <summary>The largest call number: the last group of the id holds 48 bits.</summary>
    public const long MaxCall = 0xFFFF_FFFF_FFFF;

    /// <summary>The scope.</summary>
    public uint Scope { get; } = scope;

    /// <summary>How many ids this generator has issued.</summary>
    public long Issued => Interlocked.Read(ref _issued);

    /// <inheritdoc />
    public Guid NewId() => IdAt(Interlocked.Increment(ref _issued), Scope);

    /// <summary>The id the n-th call (1-based) of a generator in <paramref name="scope"/> returns.</summary>
    /// <param name="n">The call number, 1 or more.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The id.</returns>
    public static Guid IdAt(long n, uint scope = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(n, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(n, MaxCall);

        // The call number fills the last group (48 bits); the scope the first.
        return new Guid(
            scope, 0, 0, 0, 0,
            (byte)(n >> 40), (byte)(n >> 32), (byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n);
    }
}
