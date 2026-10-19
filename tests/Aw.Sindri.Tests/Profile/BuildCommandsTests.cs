using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>
/// The profile's 'workflow.build.commands' key (line spec LN-WF9, Appendix D): the repo's typecheck, unit lane and lint as argv lists, each run on 'red', 'checks' (the default) or 'green'.
/// A profile without the key keeps its canonical JSON, so an earlier approval still holds.
/// </summary>
public sealed class BuildCommandsTests
{
    private static readonly SindriProfile _valid = new(1, "thor");

    private static SindriProfile With(params BuildCommand[] commands) => _valid with { Workflow = new(Build: new BuildSettings(commands)) };

    [Fact, Trait("Lane", "unit")]
    public void Valid_commands_are_accepted_whatever_stage_they_name()
    {
        var profile = With(
            new BuildCommand("typecheck", ["npm", "run", "typecheck"]),
            new BuildCommand("unit", ["dotnet", "test"], Timeout: "30m", On: BuildCommand.Green),
            new BuildCommand("red-tests", ["dotnet", "test", "--filter"], On: BuildCommand.Red));

        Assert.True(ProfileRules.Check(profile).IsSuccess);
        Assert.Equal([BuildCommand.Checks, BuildCommand.Green, BuildCommand.Red], profile.Workflow!.Build!.Commands!.Select(static c => c.Where), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("Type Check", "workflow.build.commands[0].name")]
    [InlineData("", "workflow.build.commands[0].name")]
    public void A_name_that_is_not_kebab_case_is_refused(string name, string key)
    {
        var error = ProfileRules.Check(With(new BuildCommand(name, ["npm", "test"]))).Error;

        Assert.Contains(key, error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_name_used_twice_is_refused()
    {
        var error = ProfileRules.Check(With(new BuildCommand("unit", ["a"]), new BuildCommand("unit", ["b"], On: BuildCommand.Green))).Error;

        Assert.Contains("workflow.build.commands[1].name", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("npm, ")]
    public void A_command_with_no_program_or_a_blank_argument_is_refused(string run)
    {
        var argv = run.Length == 0 ? [] : run.Split(',').Select(static a => a.StartsWith(' ') ? a[1..] : a).ToArray();   // "" is no argv at all; "npm, " is npm and a blank argument
        var error = ProfileRules.Check(With(new BuildCommand("unit", argv))).Error;

        Assert.Contains("workflow.build.commands[0].run", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("soon", null, "workflow.build.commands[0].timeout")]
    [InlineData(null, "ship", "workflow.build.commands[0].on")]
    public void A_bad_timeout_or_stage_is_refused_naming_the_key(string? timeout, string? on, string key)
    {
        var error = ProfileRules.Check(With(new BuildCommand("unit", ["a"], timeout, on))).Error;

        Assert.Contains(key, error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_diff_names_each_command_that_is_added_or_changes()
    {
        var live = ProfileRules.Check(With(new BuildCommand("unit", ["a"]))).Value;

        Assert.Equal(["workflow.build.commands.lint"], ProfileDiff.Between(live, With(new BuildCommand("unit", ["a"]), new BuildCommand("lint", ["b"]))));
        Assert.Equal(["workflow.build.commands.unit"], ProfileDiff.Between(live, With(new BuildCommand("unit", ["a", "--fast"]))));
        Assert.Equal(["workflow.build.commands.unit"], ProfileDiff.Between(live, With(new BuildCommand("unit", ["a"], On: BuildCommand.Green))));
        Assert.Empty(ProfileDiff.Between(live, With(new BuildCommand("unit", ["a"]))));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_without_the_key_keeps_its_canonical_json_and_hash()
    {
        var before = ProfileRules.Check(_valid with { Workflow = new("1m") }).Value;
        var json = AwJson.Serialize(before, SliceHarness.Json);

        Assert.DoesNotContain("build", Encoding.UTF8.GetString(json), StringComparison.Ordinal);
        Assert.NotEqual(
            ProfileHashing.Of(json),
            ProfileHashing.Of(AwJson.Serialize(before with { Workflow = new("1m", Build: new BuildSettings([new BuildCommand("unit", ["a"])])) }, SliceHarness.Json)));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_yaml_profile_reads_the_commands_and_refuses_an_unknown_field()
    {
        using var files = new ProfileFiles();
        files.Write("schemaVersion: 1\nuser: thor\nworkflow:\n  build:\n    commands:\n      - name: typecheck\n        run: [npm, run, typecheck]\n      - name: unit\n        run: [dotnet, test]\n        timeout: 30m\n        on: green\n");
        var loaded = await files.Source.Load(TestContext.Current.CancellationToken);

        Assert.False(loaded.IsError, loaded.IsError ? loaded.Error.Message : null);
        var commands = loaded.Value.Profile.Workflow!.Build!.Commands!;
        Assert.Equal(["typecheck", "unit"], commands.Select(static c => c.Name), StringComparer.Ordinal);
        Assert.Equal(["dotnet", "test"], commands[1].Run);
        Assert.Equal(("30m", BuildCommand.Green), (commands[1].Timeout, commands[1].Where));

        files.Write("schemaVersion: 1\nuser: thor\nworkflow:\n  build:\n    commands:\n      - name: unit\n        run: [a]\n        shell: true\n");
        var refused = await files.Source.Load(TestContext.Current.CancellationToken);

        Assert.True(refused.IsError);
    }
}
