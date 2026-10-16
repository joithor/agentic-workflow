using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>One program call (no shell): the program, then its arguments.</summary>
/// <param name="Argv">argv[0] is a path under a build output or an allowed path, never a bare name.</param>
/// <param name="Expect">What the call must produce; its exit code is required.</param>
/// <param name="Stdin">Text written to its standard input.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CliStep(IReadOnlyList<string> Argv, Expectation Expect, string? Stdin = null);
