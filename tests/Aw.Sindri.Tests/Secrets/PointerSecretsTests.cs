using Aw.Sindri.Core;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Secrets;

public sealed class PointerSecretsTests
{
    private const string Fixture = "fixture-token-value";

    [Fact, Trait("Lane", "unit")]
    public void An_env_pointer_reads_its_variable()
    {
        var secrets = Secrets(name => name == "ONYX_TOKEN" ? Fixture : null);

        Assert.Equal(Fixture, secrets.Resolve(Pointer("env:ONYX_TOKEN")).Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("")]
    public void An_unset_or_empty_variable_is_SND_SECRET_002(string? value)
    {
        var result = Secrets(_ => value).Resolve(Pointer("env:ONYX_TOKEN"));

        Assert.Equal(SindriErrors.SecretUnresolved.Code, result.Error.Code);
        Assert.Contains("env:ONYX_TOKEN", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_env_pointer_never_falls_back_to_another_variable()
    {
        var result = Secrets(name => name == "LINEAR_API_KEY" ? Fixture : null).Resolve(Pointer("env:ONYX_TOKEN"));

        Assert.Equal(SindriErrors.SecretUnresolved.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_keychain_pointer_runs_security_with_the_exact_arguments()
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, Fixture + "\n"));

        var result = Secrets(_ => null, runner).Resolve(Pointer("keychain:onyx/sindri-onyx"));

        Assert.Equal(Fixture, result.Value);
        var call = Assert.Single(runner.Calls);
        Assert.Equal("security", call.FileName);
        Assert.Equal(["find-generic-password", "-s", "onyx", "-a", "sindri-onyx", "-w"], call.Arguments);
        Assert.Equal(TimeSpan.FromSeconds(5), call.Timeout);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_op_pointer_runs_op_read_with_the_exact_arguments()
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, Fixture + "\n"));

        var result = Secrets(_ => null, runner).Resolve(Pointer("op:Work/Onyx/credential"));

        Assert.Equal(Fixture, result.Value);
        var call = Assert.Single(runner.Calls);
        Assert.Equal("op", call.FileName);
        Assert.Equal(["read", "op://Work/Onyx/credential"], call.Arguments);
        Assert.Equal(TimeSpan.FromSeconds(30), call.Timeout);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("keychain:onyx/sindri-onyx", "exit 44")]
    [InlineData("keychain:onyx/sindri-onyx", "empty")]
    [InlineData("keychain:onyx/sindri-onyx", "newline only")]
    [InlineData("keychain:onyx/sindri-onyx", "missing")]
    [InlineData("keychain:onyx/sindri-onyx", "timed out")]
    [InlineData("op:Work/Onyx/credential", "exit 1")]
    [InlineData("op:Work/Onyx/credential", "empty")]
    [InlineData("op:Work/Onyx/credential", "missing")]
    [InlineData("op:Work/Onyx/credential", "timed out")]
    public void A_tool_that_fails_is_SND_SECRET_002_and_names_the_pointer(string text, string run)
    {
        var result = Secrets(_ => null, new FakeToolRunner(Run(run))).Resolve(Pointer(text));

        Assert.Equal(SindriErrors.SecretUnresolved.Code, result.Error.Code);
        Assert.Contains(text, result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Fixture, result.Error.Message, StringComparison.Ordinal);
    }

    internal static SecretPointer Pointer(string text) => SecretPointer.Parse(text).Value;

    private static ToolRun Run(string kind) => kind switch
    {
        "exit 44" => new ToolRun(ToolOutcome.Exited, 44, ""),
        "exit 1" => new ToolRun(ToolOutcome.Exited, 1, Fixture),
        "empty" => new ToolRun(ToolOutcome.Exited, 0, ""),
        "newline only" => new ToolRun(ToolOutcome.Exited, 0, "\n"),
        "missing" => ToolRun.Missing,
        _ => ToolRun.TimedOut,
    };

    private static PointerSecrets Secrets(Func<string, string?> environment, FakeToolRunner? runner = null)
        => new(environment, runner ?? new FakeToolRunner(ToolRun.Missing));
}
