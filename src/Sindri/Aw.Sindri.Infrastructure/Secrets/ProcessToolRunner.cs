namespace Aw.Sindri.Infrastructure.Secrets;

/// <summary>IToolRunner over System.Diagnostics.Process.</summary>
internal sealed class ProcessToolRunner : IToolRunner
{
    /// <inheritdoc />
    public ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout) => throw new NotSupportedException("red: run the tool");
}
