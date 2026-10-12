using Aw.Sindri.Infrastructure.Secrets;

namespace Aw.Sindri.Tests.Secrets;

/// <summary>Records each tool call and answers with a fixed run; never starts a process.</summary>
internal sealed class FakeToolRunner(ToolRun answer) : IToolRunner
{
    public List<(string FileName, IReadOnlyList<string> Arguments, TimeSpan Timeout)> Calls { get; } = [];

    public ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        Calls.Add((fileName, [.. arguments], timeout));
        return answer;
    }
}
