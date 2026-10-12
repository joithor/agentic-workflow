using Aw.Kernel.Testing;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.RegisterMcp;

[Lane(TestLane.Unit)]
public sealed class FakeClaudeCliContractTests : ClaudeCliContract
{
    private readonly FakeClaudeCli _claude = new();

    protected override IClaudeCli Create(int? exitCode, string error)
    {
        var lines = error.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        _claude.Answer = exitCode is { } code ? new ClaudeRun(code, lines.Length == 0 ? string.Empty : lines[^1]) : null;
        return _claude;
    }

    protected override IReadOnlyList<(IReadOnlyList<string> Arguments, string Directory)> Received() => _claude.Runs;

    protected override string Directory() => "/work/demo";
}
