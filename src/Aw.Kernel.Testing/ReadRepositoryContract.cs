using Xunit;

namespace Aw.Kernel.Testing;

/// <summary>
/// The read contract every repository honours, run against the in-memory fake and the SQL implementation alike,
/// so the fake can't drift (spec §4b.5). A derived test class binds the members below to one implementation and
/// carries its lane: '[Lane(TestLane.Unit)]' for the fake, '[Lane(TestLane.Integration)]' for SQL.
/// </summary>
/// <typeparam name="TEntity">The aggregate.</typeparam>
/// <typeparam name="TId">Its strongly typed id.</typeparam>
public abstract class ReadRepositoryContract<TEntity, TId>
    where TEntity : notnull
    where TId : struct, IEquatable<TId>
{
    /// <summary>Stores <paramref name="entity"/> through the implementation's write path or test seam.</summary>
    /// <param name="entity">The entity.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>When it is stored.</returns>
    protected abstract ValueTask Seed(TEntity entity, CancellationToken cancellationToken);

    /// <summary>Calls the implementation's 'Get'.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The entity, or an error.</returns>
    protected abstract ValueTask<Result<TEntity>> Get(TId id, CancellationToken cancellationToken);

    /// <summary>Calls the implementation's 'Exists'.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether it exists.</returns>
    protected abstract ValueTask<bool> Exists(TId id, CancellationToken cancellationToken);

    /// <summary>The entity's id.</summary>
    /// <param name="entity">The entity.</param>
    /// <returns>Its id.</returns>
    protected abstract TId IdOf(TEntity entity);

    /// <summary>A new, valid entity, deterministic in <paramref name="seed"/> and distinct for each seed.</summary>
    /// <param name="seed">1, 2, 3…</param>
    /// <returns>The entity.</returns>
    protected abstract TEntity NewEntity(int seed);

    /// <summary>An id no seeded entity has.</summary>
    /// <returns>The id.</returns>
    protected abstract TId MissingId();

    /// <summary>A seeded entity comes back from 'Get' equal to what was stored.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public Task Get_returns_the_seeded_entity() => throw new NotSupportedException("red: Aw.Kernel.Testing C2");

    /// <summary>Each of several seeded entities comes back by its own id.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public Task Get_returns_each_entity_by_its_own_id() => throw new NotSupportedException("red: Aw.Kernel.Testing C2");

    /// <summary>'Get' of an id nobody stored is a not-found error with a catalogue code, never an exception.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public Task Get_of_a_missing_id_is_a_not_found_error() => throw new NotSupportedException("red: Aw.Kernel.Testing C2");

    /// <summary>'Exists' is true for a seeded id and false for a missing one.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public Task Exists_is_true_only_for_seeded_ids() => throw new NotSupportedException("red: Aw.Kernel.Testing C2");
}
