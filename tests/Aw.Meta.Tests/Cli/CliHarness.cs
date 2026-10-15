using Aw.Kernel.Json;
using Aw.Kernel.Telemetry;
using Aw.Meta.Host;
using Aw.Meta.Host.Commands;
using Aw.Meta.Tests.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Meta.Tests.Cli;

/// <summary>Runs aw-meta commands in process the way Program.cs does, over a temp state directory and a fake clock; each call is a fresh container, like a fresh process.</summary>
internal sealed class CliHarness : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly Dictionary<string, string> _env = new(StringComparer.Ordinal);

    internal CliHarness()
    {
        _env["AW_STATE_DIR"] = _root.Path;
        _env["AW_CLAUDE_SESSIONS_DIR"] = SessionsDirectory;
        _ = Directory.CreateDirectory(SessionsDirectory);
    }

    internal FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 15, 5, 0, 0, TimeSpan.Zero));

    internal string SessionsDirectory => Path.Combine(_root.Path, "sessions");

    internal string File(string name) => Path.Combine(_root.Path, name);

    internal void SetEnv(string name, string value) => _env[name] = value;

    internal Task<CliResult> Run(params string[] args) => RunWithInput(string.Empty, args);

    internal async Task<CliResult> RunWithInput(string stdin, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var input = new StringReader(stdin);
        var io = new ConsoleIo(output, error, input, name => _env.GetValueOrDefault(name));
        var services = Composition.Compose(new ServiceCollection(), io.Env, Clock);
        var provider = services.BuildServiceProvider();
        await using (provider.ConfigureAwait(false))
        {
            _ = provider.GetRequiredService<TelemetryRuntime>();
            var exit = await CommandTable.Run(args, provider, AwJson.CreateOptions(MetaJsonContext.Default), io).ConfigureAwait(false);
            return new CliResult(exit, output.ToString(), error.ToString());
        }
    }

    public void Dispose() => _root.Dispose();
}
