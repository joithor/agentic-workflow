using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using EngineLaunchId = Aw.Sindri.Core.Workflow.Engine.LaunchId;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The real <see cref="ILauncher"/> (API P10a, unit U7): catalog, host and viewer composed. A red stub until U7: every member
/// throws, so <c lang="csharp">LauncherContract</c> fails against it by design.
/// </summary>
[SuppressMessage("Major Code Smell", "MA0182:Avoid unused internal types", Justification = "Registered in U7 (API P10a); a red stub until then.")]
internal sealed class Launcher : ILauncher
{
    /// <inheritdoc/>
    public ValueTask<LaunchResult> Launch(LaunchRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Launcher: make LauncherContract pass (U7)");

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Stop(EngineLaunchId launch, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Launcher: make LauncherContract pass (U7)");

    /// <inheritdoc/>
    public ValueTask<LaunchProbed> Probe(CancellationToken cancellationToken) =>
        throw new NotSupportedException("Launcher: make LauncherContract pass (U7)");
}
