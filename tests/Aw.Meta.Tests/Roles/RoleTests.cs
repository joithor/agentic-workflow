using Aw.Meta.Core.Ids;
using Aw.Meta.Features.Roles;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Roles;

public sealed class RoleTests
{
    private readonly InMemoryMessageStore _store = new();

    private SetRoleService Set() => new(_store);

    private ShowRoleService Show() => new(_store);

    [Fact, Trait("Lane", "unit")]
    public async Task SetRole_registers_the_session_and_ShowRole_reads_it_back()
    {
        var set = await Set().Handle(new SetRoleRequest(SessionId.From("s-1"), "t1-meta"), CancellationToken.None);
        var shown = await Show().Handle(new ShowRoleRequest(), CancellationToken.None);

        Assert.True(set.IsSuccess);
        Assert.Equal("t1-meta", shown.Value.Role?.Name);
        Assert.Equal("s-1", shown.Value.Role?.Session.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_SetRole_replaces_the_first()
    {
        _ = await Set().Handle(new SetRoleRequest(SessionId.From("s-1"), "old"), CancellationToken.None);
        _ = await Set().Handle(new SetRoleRequest(SessionId.From("s-2"), string.Empty), CancellationToken.None);

        var shown = await Show().Handle(new ShowRoleRequest(), CancellationToken.None);

        Assert.Equal("s-2", shown.Value.Role?.Session.Value);
        Assert.Equal(string.Empty, shown.Value.Role?.Name);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ShowRole_with_no_registered_session_has_no_role()
    {
        var shown = await Show().Handle(new ShowRoleRequest(), CancellationToken.None);

        Assert.True(shown.IsSuccess);
        Assert.Null(shown.Value.Role);
    }
}
