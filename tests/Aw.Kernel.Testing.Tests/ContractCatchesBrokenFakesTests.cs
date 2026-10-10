
namespace Aw.Kernel.Testing.Tests;

/// <summary>
/// The contract must fail an implementation that drifts. Each broken fake below is private, so xUnit doesn't
/// discover it as a test class; these tests call its inherited contract tests directly and expect a violation.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ContractCatchesBrokenFakesTests
{
    [Fact]
    public async Task A_fake_that_throws_for_a_missing_id_breaks_the_contract()
    {
        var contract = new ThrowsOnMissing();

        var e = await Assert.ThrowsAsync<ContractViolationException>(contract.Get_of_a_missing_id_is_a_not_found_error);

        Assert.Contains("Get_of_a_missing_id_is_a_not_found_error", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_fake_that_returns_the_wrong_error_kind_breaks_the_contract()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(new WrongErrorKind().Get_of_a_missing_id_is_a_not_found_error);

        Assert.Contains("notFound", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_fake_that_returns_the_first_entity_for_every_id_breaks_the_contract() =>
        await Assert.ThrowsAsync<ContractViolationException>(new AlwaysFirst().Get_returns_each_entity_by_its_own_id);

    [Fact]
    public async Task A_fake_whose_exists_is_always_true_breaks_the_contract() =>
        await Assert.ThrowsAsync<ContractViolationException>(new ExistsAlwaysTrue().Exists_is_true_only_for_seeded_ids);

    [Fact]
    public async Task A_fake_that_loses_what_was_stored_breaks_the_contract() =>
        await Assert.ThrowsAsync<ContractViolationException>(new Forgetful().Get_returns_the_seeded_entity);

    [Fact]
    public async Task A_fake_that_throws_for_a_stored_id_breaks_the_contract()
    {
        var e = await Assert.ThrowsAsync<ContractViolationException>(new ThrowsAlways().Get_returns_the_seeded_entity);

        _ = Assert.IsType<InvalidOperationException>(e.InnerException);
    }

    [Fact]
    public async Task A_fake_whose_exists_throws_breaks_the_contract() =>
        await Assert.ThrowsAsync<ContractViolationException>(new ExistsThrows().Exists_is_true_only_for_seeded_ids);

    private class Broken : ReadRepositoryContract<Item, ItemId>
    {
        protected InMemoryItemRepository Repository { get; } = new();

        protected override ValueTask Seed(Item entity, CancellationToken cancellationToken)
        {
            Repository.Save(entity);
            return ValueTask.CompletedTask;
        }

        protected override ValueTask<Result<Item>> Get(ItemId id, CancellationToken cancellationToken) => Repository.Get(id);

        protected override ValueTask<bool> Exists(ItemId id, CancellationToken cancellationToken) => Repository.Exists(id);

        protected override ItemId IdOf(Item entity) => entity.Id;

        protected override Item NewEntity(int seed) => A.Item().WithSeed(seed).Build();

        protected override ItemId MissingId() => ItemId.From(999);
    }

    private sealed class ThrowsOnMissing : Broken
    {
        protected override async ValueTask<Result<Item>> Get(ItemId id, CancellationToken cancellationToken)
        {
            var found = await Repository.Get(id);
            return found.IsSuccess ? found : throw new KeyNotFoundException("no such item");
        }
    }

    private sealed class WrongErrorKind : Broken
    {
        protected override async ValueTask<Result<Item>> Get(ItemId id, CancellationToken cancellationToken)
        {
            var found = await Repository.Get(id);
            return found.IsSuccess ? found : KernelErrors.Invalid.With("bad id");
        }
    }

    private sealed class AlwaysFirst : Broken
    {
        protected override ValueTask<Result<Item>> Get(ItemId id, CancellationToken cancellationToken) =>
            Repository.Get(ItemId.From(1));
    }

    private sealed class ExistsAlwaysTrue : Broken
    {
        protected override ValueTask<bool> Exists(ItemId id, CancellationToken cancellationToken) => ValueTask.FromResult(true);
    }

    private sealed class ThrowsAlways : Broken
    {
        protected override ValueTask<Result<Item>> Get(ItemId id, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("database is gone");
    }

    private sealed class ExistsThrows : Broken
    {
        protected override ValueTask<bool> Exists(ItemId id, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("database is gone");
    }

    private sealed class Forgetful : Broken
    {
        protected override ValueTask Seed(Item entity, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
