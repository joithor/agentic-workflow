namespace Aw.Kernel.Ids;

/// <summary>Typed ids from the <see cref="IIdGenerator"/> port.</summary>
public static class IdGeneratorExtensions
{
    /// <summary>Returns a new <typeparamref name="TId"/> wrapping the port's next id.</summary>
    /// <param name="generator">The id port.</param>
    /// <typeparam name="TId">A Guid-backed typed id.</typeparam>
    /// <returns>The new id.</returns>
    /// <exception cref="InvalidOperationException">The port produced a value the id type refuses (a bug in the port).</exception>
    public static TId New<TId>(this IIdGenerator generator)
        where TId : struct, ITypedId<TId, Guid>
    {
        ArgumentNullException.ThrowIfNull(generator);
        return TId.TryFrom(generator.NewId(), out var id)
            ? id
            : throw new InvalidOperationException($"The id generator produced a value {typeof(TId).Name} refuses.");
    }
}
