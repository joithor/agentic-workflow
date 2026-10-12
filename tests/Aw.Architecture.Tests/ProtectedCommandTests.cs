using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;

namespace Aw.Architecture.Tests;

/// <summary>
/// The protected commands (engine spec §4.2, §6.2 W22; API spec §4.1): every 'protected' catalog row is bound to a
/// terminal and its request redeems the server's challenge, and no client that can't sit at a terminal (the Avalonia
/// app, an MCP server) references a protected request or route. Read from the Sindri host's catalog, the one table the
/// command chain and the API both read.
/// </summary>
[Trait("Lane", "unit")]
public sealed class ProtectedCommandTests
{
    /// <summary>
    /// Protected rows whose request still confirms another way. S1-D5 replaces the profile approval's 'confirmed' flag
    /// with the challenge in P2; delete the entry when it lands (a stale entry fails the test).
    /// </summary>
    private static readonly string[] _notYetChallenged = ["ApproveProfile"];

    [Fact]
    public void Every_protected_engine_command_requires_the_challenge()
    {
        var rows = Catalog().Rows.Where(r => r.Class == ActionClass.Protected).ToList();

        var unbound = rows.Where(r => !r.NeedsTerminal).Select(r => $"{r.Action.Value} isn't terminal-bound").ToList();
        var unchallenged = rows
            .Where(r => r.CommandType is { } type && !Redeems(type))
            .Select(r => r.Action.Value)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(unbound.Count == 0, string.Join(Environment.NewLine, unbound) + ". Fix: set NeedsTerminal on every protected row (engine spec §6.2)");
        Assert.True(
            unchallenged.SequenceEqual(_notYetChallenged, StringComparer.Ordinal),
            $"protected requests without a {nameof(ChallengeAnswer)}: [{string.Join(", ", unchallenged)}], expected only [{string.Join(", ", _notYetChallenged)}]. "
            + $"Fix: give the request a {nameof(ChallengeAnswer)} it redeems (engine spec §4.2), or delete a stale entry from the allowlist");
    }

    [Fact]
    public void No_client_references_a_protected_request_or_route()
    {
        var rows = Catalog().Rows.Where(r => r.Class == ActionClass.Protected).ToList();
        var types = rows.Select(r => r.CommandType?.FullName).OfType<string>().Append(typeof(ChallengeAnswer).FullName).ToHashSet(StringComparer.Ordinal);
        var routes = rows.Select(r => r.Route).OfType<string>().SelectMany(r => new[] { r, r.Replace("/sindri", string.Empty, StringComparison.Ordinal) }).ToHashSet(StringComparer.Ordinal);

        var found = Production.Architecture.Types
            .Where(t => IsClient(t.Assembly.Name))
            .SelectMany(t => t.Dependencies.Where(d => types.Contains(d.Target.FullName)).Select(d => $"{t.FullName} uses {d.Target.FullName}"))
            .Concat(Production.Assemblies.Where(a => IsClient(a.GetName().Name ?? string.Empty)).SelectMany(a => Strings(a).Where(routes.Contains).Select(s => $"{a.GetName().Name} holds route '{s}'")))
            .ToList();

        Assert.True(found.Count == 0, string.Join(Environment.NewLine, found)
            + ". Fix: a protected action is typed at a terminal; the app shows the CLI form to copy (engine spec §4.2, UI spec E-9)");
    }

    /// <summary>The Avalonia app (SindriUi, spec §4a 'UI') and any MCP server: clients that never sit at a terminal.</summary>
    private static bool IsClient(string assembly) =>
        assembly.StartsWith("Aw.Sindri.Ui", StringComparison.Ordinal) || assembly.Contains(".Mcp", StringComparison.Ordinal);

    private static bool Redeems(Type request) =>
        request.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(p => p.PropertyType == typeof(ChallengeAnswer));

    /// <summary>The Sindri host's command catalog (internal to the host, so made by name).</summary>
    private static ICommandCatalog Catalog()
    {
        var host = Production.Assemblies.Single(a => a.GetName().Name == Solution.AssemblyOf("Aw.Sindri.Host"));
        var type = host.GetType("Aw.Sindri.Host.Catalog.CommandCatalog", throwOnError: true)!;
        return (ICommandCatalog)Activator.CreateInstance(type, nonPublic: true)!;
    }

    /// <summary>The string literals compiled into <paramref name="assembly"/> (its user-string heap).</summary>
    private static List<string> Strings(Assembly assembly)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var strings = new List<string>();
        if (metadata.GetHeapSize(HeapIndex.UserString) <= 1)
        {
            return strings;
        }

        for (var handle = MetadataTokens.UserStringHandle(1); !handle.IsNil; handle = metadata.GetNextHandle(handle))
        {
            strings.Add(metadata.GetUserString(handle));
        }

        return strings;
    }
}
