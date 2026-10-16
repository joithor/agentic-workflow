using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Testing;

/// <summary>A runner arranged by a <see cref="CliRunnerContract"/> subclass and the program path to run.</summary>
/// <param name="Runner">The runner under test.</param>
/// <param name="Executable">The program to run (absolute).</param>
public sealed record CliUnderTest(ICliRunner Runner, string Executable);
