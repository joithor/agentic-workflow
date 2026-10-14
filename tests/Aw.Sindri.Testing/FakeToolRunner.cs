using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Infrastructure.Secrets;

namespace Aw.Sindri.Testing;

/// <summary>Records each tool call and answers with a fixed run; never starts a process. Held to ToolRunnerContract.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Internal because IToolRunner is; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can't see.")]
internal sealed class FakeToolRunner(ToolRun answer) : IToolRunner
{
    public List<(string FileName, IReadOnlyList<string> Arguments, TimeSpan Timeout)> Calls { get; } = [];

    public ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        Calls.Add((fileName, [.. arguments], timeout));
        return answer;
    }
}
