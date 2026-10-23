namespace Aw.Evidence.Host.Commands;

/// <summary>Stub (cutover unit 1.4 fills it): the 'version' command prints the build stamp, which is Sindri's stamp (SourceRevisionId and AwBuildTime).</summary>
internal static class VersionCommand
{
    internal const string Name = "version";
    internal const string Shape = "version";

    private static readonly CliSpec _spec = new(0, [], []);

    internal static Task<int> Run(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        if (CliArguments.Parse(args, _spec) is null)
        {
            return Task.FromResult(CommandOutput.Usage(Shape));
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("version is not built yet (cutover unit 1.4)");
    }
}
