using Aw.Meta.Host.Commands;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Team;

public sealed class TeamResolverTests : IDisposable
{
    private readonly TempDirectory _root = new();

    private string Meta => _root.Path;

    private void Named(string name)
    {
        _ = Directory.CreateDirectory(Path.Combine(Meta, "teams", name));
        File.WriteAllText(Path.Combine(Meta, "teams", name, "team.json"), $$"""{"team":"{{name}}","repo":"/r","lanes":["a"]}""");
    }

    private void Legacy(string team) => File.WriteAllText(Path.Combine(Meta, "team.json"), $$"""{"team":"{{team}}","repo":"/r","lanes":["a"]}""");

    [Fact, Trait("Lane", "unit")]
    public void With_nothing_on_disk_and_nothing_selected_the_flat_directory_is_used_as_before()
    {
        var (state, layout) = TeamResolver.Resolve(Meta, selected: null);

        Assert.Equal((Meta, true, 0), (state, layout.Legacy, layout.Ambiguous.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_legacy_config_is_used_unless_another_team_is_selected_and_its_own_name_selects_it()
    {
        Legacy("t2");
        Named("other");

        Assert.Equal(Meta, TeamResolver.Resolve(Meta, selected: null).StateDirectory);
        Assert.Equal(Meta, TeamResolver.Resolve(Meta, "t2").StateDirectory);
        Assert.Equal(Path.Combine(Meta, "teams", "other"), TeamResolver.Resolve(Meta, "other").StateDirectory);
        Assert.Equal(Path.Combine(Meta, "teams", "fresh"), TeamResolver.Resolve(Meta, "fresh").StateDirectory);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_named_team_beats_a_legacy_config_of_the_same_name_only_when_selected()
    {
        Legacy("t2");
        Named("t2");

        Assert.Equal(Meta, TeamResolver.Resolve(Meta, selected: null).StateDirectory);
        Assert.Equal(Path.Combine(Meta, "teams", "t2"), TeamResolver.Resolve(Meta, "t2").StateDirectory);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_empty_team_directory_without_a_config_does_not_hide_the_legacy_team()
    {
        Legacy("t2");
        _ = Directory.CreateDirectory(Path.Combine(Meta, "teams", "t2"));

        Assert.Equal(Meta, TeamResolver.Resolve(Meta, "t2").StateDirectory);
    }

    [Fact, Trait("Lane", "unit")]
    public void One_named_team_is_found_and_several_are_ambiguous_until_one_is_selected()
    {
        Named("beta");
        Assert.Equal(Path.Combine(Meta, "teams", "beta"), TeamResolver.Resolve(Meta, selected: null).StateDirectory);

        Named("alpha");
        var (_, layout) = TeamResolver.Resolve(Meta, selected: null);

        Assert.Equal(["alpha", "beta"], layout.Ambiguous);
        Assert.Empty(TeamResolver.Resolve(Meta, "alpha").Layout.Ambiguous);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("../x")]
    [InlineData("a/b")]
    [InlineData("A")]
    [InlineData("-x")]
    [InlineData("a b")]
    public void A_name_that_is_not_a_plain_word_is_a_problem_and_never_a_path(string name)
    {
        var (state, layout) = TeamResolver.Resolve(Meta, name);

        Assert.NotNull(layout.Problem);
        Assert.Equal(Meta, state);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_team_option_moves_into_the_environment_and_out_of_the_arguments_except_for_team_init()
    {
        var (args, env, problem) = TeamOption.Apply(["--team", "beta", "inbox", "--json"], _ => "from-env");
        var (initArgs, initEnv, _) = TeamOption.Apply(["team-init", "--team", "x", "--repo", "/r"], _ => null);
        var (_, _, missing) = TeamOption.Apply(["inbox", "--team"], _ => null);

        Assert.Equal(["inbox", "--json"], args);
        Assert.Equal(("beta", string.Empty), (env("AW_META_TEAM"), problem));
        Assert.Equal(["team-init", "--repo", "/r", "--team", "x"], initArgs);
        Assert.Equal(["team-init", "--repo", "/r", "--team", "y"], TeamOption.Apply(["--team", "y", "team-init", "--repo", "/r"], _ => null).Args);
        Assert.NotEmpty(TeamOption.Apply(["--team", "a", "inbox", "--team", "b"], _ => null).Problem);
        Assert.Equal("x", initEnv("AW_META_TEAM"));
        Assert.NotEmpty(missing);
    }

    public void Dispose() => _root.Dispose();
}
