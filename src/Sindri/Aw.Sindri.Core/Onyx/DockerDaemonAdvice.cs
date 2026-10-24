using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>What Sindri tells a person about a Docker daemon that doesn't answer, by the runtime that serves it (<see cref="ContainerRuntimeFacts.Kind"/>): the line `onyx up` shows while it waits, and the docker row's finding and fix.</summary>
public static class DockerDaemonAdvice
{
    /// <summary>The runtime's name as a message says it.</summary>
    /// <param name="kind">The runtime.</param>
    /// <returns>OrbStack, Docker Desktop, or Docker.</returns>
    public static string Name(ContainerRuntimeKind kind) => kind switch
    {
        ContainerRuntimeKind.OrbStack => "OrbStack",
        ContainerRuntimeKind.DockerDesktop => "Docker Desktop",
        ContainerRuntimeKind.Docker => "Docker",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown runtime"),
    };

    /// <summary>The line `onyx up` shows when it starts waiting for a daemon that <see cref="ContainerRuntimeFacts.DaemonMayStillStart"/>: what it waits for, how long, and what the person can do meanwhile.</summary>
    /// <param name="docker">What the runtime reported.</param>
    /// <param name="window">How long it waits.</param>
    /// <returns>The line, without a newline.</returns>
    public static string WaitLine(ContainerRuntimeFacts docker, TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(docker);
        var name = Name(docker.Kind);
        var minutes = string.Create(CultureInfo.InvariantCulture, $"{window.TotalMinutes:0.#} minutes");
        return docker.DaemonProblem == DaemonProblem.Starting
            ? $"{name} isn't answering yet; waiting up to {minutes} for it ({HungFix(docker.Kind)}; Ctrl-C stops)"
            : $"{name} isn't running yet; waiting up to {minutes} for it to start ({StartFix(docker.Kind)}; Ctrl-C stops)";
    }

    /// <summary>The docker row's finding and SND-ONYX-001 for a daemon that didn't answer in time.</summary>
    /// <param name="docker">What the runtime reported.</param>
    /// <returns>The row's detail and the error.</returns>
    internal static (string Detail, Error Problem) NotAnswering(ContainerRuntimeFacts docker)
    {
        var name = Name(docker.Kind);
        var seconds = (docker.WaitedSeconds ?? 0).ToString(CultureInfo.InvariantCulture);
        return ($"{name} didn't answer within {seconds} s; it may be starting",
            SindriErrors.OnyxDockerUnavailable.With($"{name} isn't answering (no reply within {seconds} s): {HungFix(docker.Kind)}, then run this again"));
    }

    /// <summary>The docker row's finding and SND-ONYX-001 for a daemon that isn't running.</summary>
    /// <param name="docker">What the runtime reported.</param>
    /// <returns>The row's detail and the error.</returns>
    internal static (string Detail, Error Problem) NotRunning(ContainerRuntimeFacts docker)
    {
        var waited = docker.WaitedSeconds is { } seconds ? $" after {seconds.ToString(CultureInfo.InvariantCulture)} s" : "";
        return ($"docker CLI found, daemon not answering{waited}",
            SindriErrors.OnyxDockerUnavailable.With($"{Name(docker.Kind)} isn't running: {StartFix(docker.Kind)} and wait until it is ready, then run this again"));
    }

    // A daemon that hangs: OrbStack can say Running while its engine is wedged (`orb restart` clears it); Docker Desktop is often waking from Resource Saver.
    private static string HungFix(ContainerRuntimeKind kind) => kind switch
    {
        ContainerRuntimeKind.OrbStack => "open OrbStack, or run `orb restart` if it says Running",
        ContainerRuntimeKind.DockerDesktop => "open Docker Desktop (it may be waking from Resource Saver) and wait until it is ready, or restart it from its menu",
        ContainerRuntimeKind.Docker => "restart it (Docker Desktop, OrbStack, or the docker service)",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown runtime"),
    };

    private static string StartFix(ContainerRuntimeKind kind) => kind switch
    {
        ContainerRuntimeKind.OrbStack => "open OrbStack (or run `orb start`)",
        ContainerRuntimeKind.DockerDesktop => "open Docker Desktop",
        ContainerRuntimeKind.Docker => "start Docker Desktop, OrbStack or the docker service",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown runtime"),
    };
}
