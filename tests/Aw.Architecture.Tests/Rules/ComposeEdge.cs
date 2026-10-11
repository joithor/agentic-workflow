namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// A deps.yaml compose edge by package root: the Host of <paramref name="HostRoot"/> may call the public Add…()
/// registration methods of <paramref name="TargetRoot"/>'s Features and Infrastructure, and nothing else from them
/// (context spec §4; ARCH-COMPOSE-001).
/// </summary>
/// <param name="HostRoot">The composing package's root, e.g. Aw.Sindri.</param>
/// <param name="TargetRoot">The composed package's root, e.g. Aw.Context.</param>
public sealed record ComposeEdge(string HostRoot, string TargetRoot)
{
    /// <summary>The compose edges declared in a deps.yaml text, mapped to package roots by projectPrefix.</summary>
    /// <param name="depsYaml">deps.yaml's text.</param>
    /// <returns>The edges.</returns>
    public static IReadOnlyList<ComposeEdge> Parse(string depsYaml) => throw new NotSupportedException();
}
