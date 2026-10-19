using Aw.Sindri.Core.Onyx;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IOnyxDatabase"/> honours: a stack of Sindri's with a default Admin group answers its id, a home with no database container answers null, and a project name Sindri didn't make is refused. Run against the fake and the docker adapter over a scripted runner.</summary>
public abstract class OnyxDatabaseContract
{
    /// <summary>The compose project of the home the subject is asked about.</summary>
    protected static ComposeProject Ours { get; } = ComposeProject.ByLabel(ComposeProject.NameFor("/sindri-home"));

    /// <summary>A subject over a stack whose default Admin group has this id (null: no database container runs).</summary>
    /// <param name="group">The group id.</param>
    /// <returns>The subject.</returns>
    protected abstract IOnyxDatabase Create(int? group);

    /// <summary>The id comes back.</summary>
    [Fact]
    public void A_stack_with_a_default_admin_group_answers_its_id()
        => Expect.Equal(4, Create(4).DefaultAdminGroup(Ours, TestContext.Current.CancellationToken).Value);

    /// <summary>No database container: null, not an error.</summary>
    [Fact]
    public void A_home_with_no_database_container_answers_null()
    {
        var answer = Create(group: null).DefaultAdminGroup(Ours, TestContext.Current.CancellationToken);

        Expect.True(answer.IsSuccess);
        Expect.True(answer.Value is null);
    }

    /// <summary>A name Sindri didn't make is refused (the fake and the adapter both).</summary>
    [Fact]
    public void A_project_sindri_did_not_name_is_refused()
        => Expect.True(Create(4).DefaultAdminGroup(ComposeProject.ByLabel("someone-elses-stack"), TestContext.Current.CancellationToken).IsError);
}
