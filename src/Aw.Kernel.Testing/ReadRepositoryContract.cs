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
    public async Task Get_returns_the_seeded_entity()
    {
        var entity = NewEntity(1);
        await Seed(entity, Token).ConfigureAwait(false);

        var found = await Get(IdOf(entity), Token).ConfigureAwait(false);

        ExpectFound(nameof(Get_returns_the_seeded_entity), entity, found);
    }

    /// <summary>Each of several seeded entities comes back by its own id.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Get_returns_each_entity_by_its_own_id()
    {
        TEntity[] entities = [NewEntity(1), NewEntity(2), NewEntity(3)];
        foreach (var entity in entities)
        {
            await Seed(entity, Token).ConfigureAwait(false);
        }

        foreach (var entity in entities)
        {
            var found = await Get(IdOf(entity), Token).ConfigureAwait(false);
            ExpectFound(nameof(Get_returns_each_entity_by_its_own_id), entity, found);
        }
    }

    /// <summary>'Get' of an id nobody stored is a not-found error with a catalogue code, never an exception.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Get_of_a_missing_id_is_a_not_found_error()
    {
        const string test = nameof(Get_of_a_missing_id_is_a_not_found_error);
        await Seed(NewEntity(1), Token).ConfigureAwait(false);
        var missing = MissingId();
        Result<TEntity> found;
        try
        {
            found = await Get(missing, Token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            throw Violation(test, $"Get({missing}) threw {e.GetType().Name} instead of returning a notFound error.", e);
        }

        if (found.IsSuccess)
        {
            throw Violation(test, $"Get({missing}) returned an entity for an id nobody stored.");
        }

        if (found.Error.Kind != ErrorKind.NotFound || string.IsNullOrWhiteSpace(found.Error.Code))
        {
            throw Violation(test, $"Get({missing}) must return a notFound error with a catalogue code; got {found.Error.Kind} ({found.Error.Code}).");
        }
    }

    /// <summary>'Exists' is true for a seeded id and false for a missing one.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Exists_is_true_only_for_seeded_ids()
    {
        const string test = nameof(Exists_is_true_only_for_seeded_ids);
        var entity = NewEntity(1);
        await Seed(entity, Token).ConfigureAwait(false);

        if (!await Exists(IdOf(entity), Token).ConfigureAwait(false))
        {
            throw Violation(test, $"Exists({IdOf(entity)}) is false for a stored entity.");
        }

        if (await Exists(MissingId(), Token).ConfigureAwait(false))
        {
            throw Violation(test, $"Exists({MissingId()}) is true for an id nobody stored.");
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContractViolationException Violation(string test, string detail) =>
        new($"{typeof(ReadRepositoryContract<TEntity, TId>).Name.Split('`')[0]}.{test}: {detail}");

    private static ContractViolationException Violation(string test, string detail, Exception cause) =>
        new($"{typeof(ReadRepositoryContract<TEntity, TId>).Name.Split('`')[0]}.{test}: {detail}", cause);

    private void ExpectFound(string test, TEntity stored, Result<TEntity> found)
    {
        var id = IdOf(stored);
        if (found.IsError)
        {
            throw Violation(test, $"Get({id}) returned {found.Error.Kind} ({found.Error.Code}) for a stored entity.");
        }

        if (!EqualityComparer<TEntity>.Default.Equals(found.Value, stored))
        {
            throw Violation(test, $"Get({id}) returned {found.Value}, not the stored {stored}.");
        }
    }
}
