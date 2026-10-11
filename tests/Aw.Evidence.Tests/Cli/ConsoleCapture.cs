namespace Aw.Evidence.Tests.Cli;

/// <summary>The tests that swap Console's writers run one at a time, apart from every other test.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleCapture
{
    public const string Name = "console";
}
