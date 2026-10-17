using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary><c lang="text">sindri launch init</c> and <c lang="text">sindri launch show</c> (API P10a, U7; launcher spec §4 "The generator").</summary>
[SuppressMessage("Major Code Smell", "MA0182:Avoid unused internal types", Justification = "Registered in U7 (API P10a).")]
internal sealed class LauncherInit(LauncherInitParts parts) : ILaunchSetup
{
    private readonly LauncherInitParts _parts = parts;

    /// <inheritdoc/>
    public ValueTask<Result<LaunchInitReport>> Init(LaunchInitRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("LauncherInit: make the init tests pass (U7) " + _parts.Catalog.Path);

    /// <inheritdoc/>
    public ValueTask<Result<LaunchShowReport>> Show(AgentId? only, CancellationToken cancellationToken) =>
        throw new NotSupportedException("LauncherInit: make the show tests pass (U7) " + _parts.Catalog.Path);
}
