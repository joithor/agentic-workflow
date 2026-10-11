using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Profile;

namespace Aw.Sindri.Tests.Commands;

/// <summary>
/// The verb table as Program runs it, with the console swapped for writers: the exit codes the doc promises for the
/// local verbs. It swaps the process's console, so it never runs beside another test.
/// </summary>
[Collection(ProcessEnvironment.Name)]
public sealed class CommandTableTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public CommandTableTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _home.Dispose();
    }

    private string ProfilePath => Path.Combine(_home.Path, "profile", "profile.yaml");

    [Fact, Trait("Lane", "integration")]
    public async Task An_unknown_verb_exits_2_with_SND_CLI_001()
    {
        var code = await Run("frobnicate");

        Assert.Equal(2, code);
        Assert.Contains("SND-CLI-001 'frobnicate' is not a sindri command", _error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("none", 2)]
    [InlineData("invalid", 2)]
    [InlineData("valid", 1)]
    [InlineData("approved", 0)]
    public async Task Profile_validate_exits_0_approved_1_unapproved_2_invalid(string state, int expected)
    {
        await Put(state);

        Assert.Equal(expected, await Run("profile", "validate"));
    }

    private async Task Put(string state)
    {
        if (state == "none")
        {
            return;
        }

        _ = Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);   // why: the profile path has a parent
        await File.WriteAllTextAsync(ProfilePath, state == "invalid" ? "schemaVersion: 1\nuser: thor\nunknownKey: 1\n" : "schemaVersion: 1\nuser: thor\n",
            TestContext.Current.CancellationToken);
        if (state == "approved")
        {
            using var files = new ProfileFiles(_home.Path);
            _ = await files.Approvals.Save((await files.Source.Load(TestContext.Current.CancellationToken)).Value, TestContext.Current.CancellationToken);
        }
    }

    private Task<int> Run(params string[] args) =>
        CommandTable.Run(args, n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System);
}
