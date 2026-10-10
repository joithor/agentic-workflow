namespace Aw.Kernel.Testing.Tests;

[Lane(TestLane.Unit)]
public sealed class SequentialIdGeneratorTests
{
    [Fact]
    public void Ids_count_up_from_one_in_readable_form()
    {
        var ids = new SequentialIdGenerator();

        Assert.Equal("00000000-0000-0000-0000-000000000001", ids.NewId().ToString());
        Assert.Equal("00000000-0000-0000-0000-000000000002", ids.NewId().ToString());
        Assert.Equal(2, ids.Issued);
    }

    [Fact]
    public void Scopes_keep_generators_apart()
    {
        var items = new SequentialIdGenerator(scope: 1);
        var sessions = new SequentialIdGenerator(scope: 2);

        Assert.Equal("00000001-0000-0000-0000-000000000001", items.NewId().ToString());
        Assert.Equal("00000002-0000-0000-0000-000000000001", sessions.NewId().ToString());
    }

    [Fact]
    public void IdAt_predicts_the_nth_id()
    {
        var ids = new SequentialIdGenerator(scope: 7);
        _ = ids.NewId();
        _ = ids.NewId();

        Assert.Equal(SequentialIdGenerator.IdAt(3, scope: 7), ids.NewId());
    }

    [Fact]
    public void Two_generators_give_the_same_sequence() =>
        Assert.Equal(
            Enumerable.Range(0, 5).Select(_ => new SequentialIdGenerator(3)).Select(g => g.NewId()),
            Enumerable.Repeat(SequentialIdGenerator.IdAt(1, 3), 5));

    [Fact]
    public void IdAt_refuses_a_call_number_below_one() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SequentialIdGenerator.IdAt(0));
}
