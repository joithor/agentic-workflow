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
[Trait("Lane", "integration")]
public sealed class ProtectedCommandTests
{
    /// <summary>
    /// Protected rows that still confirm another way, each with its reason; a stale entry fails, and review keeps new entries out.
    /// </summary>
    private static readonly Dictionary<string, string> _confirmsOtherwise = new(StringComparer.Ordinal)
    {
    };

    [Fact]
    public void Every_protected_engine_command_requires_the_challenge()
    {
        var rows = HostIntrospection.Catalog().Rows.Where(r => r.Class == ActionClass.Protected).ToList();

        var unbound = rows.Where(r => !r.NeedsTerminal).Select(r => $"{r.Action.Value} isn't terminal-bound").ToList();
        var unchallenged = rows
            .Where(r => r.CommandType is { } type && !Redeems(type) && !_confirmsOtherwise.ContainsKey(r.Action.Value))
            .Select(r => r.Action.Value)
            .Order(StringComparer.Ordinal)
            .ToList();

        var stale = _confirmsOtherwise.Keys
            .Where(a => !rows.Exists(r => r.Action.Value == a && r.CommandType is { } type && !Redeems(type)))
            .Order(StringComparer.Ordinal)
            .ToList();
        var exempted = string.Join("; ", _confirmsOtherwise.Select(e => $"{e.Key}: {e.Value}"));

        Assert.True(unbound.Count == 0, string.Join(Environment.NewLine, unbound) + ". Fix: set NeedsTerminal on every protected row (engine spec §6.2)");
        Assert.True(stale.Count == 0, $"stale exemptions: [{string.Join(", ", stale)}]. Fix: remove them from _confirmsOtherwise (exempted: {exempted})");
        Assert.True(
            unchallenged.Count == 0,
            $"protected requests without a {nameof(ChallengeAnswer)}: [{string.Join(", ", unchallenged)}]. "
            + $"Fix: give the request a {nameof(ChallengeAnswer)} it redeems (engine spec §4.2). Exempted: {exempted}");
    }

    [Fact]
    public void No_client_references_a_protected_request_or_route()
    {
        var rows = HostIntrospection.Catalog().Rows.Where(r => r.Class == ActionClass.Protected).ToList();
        var types = rows.Select(r => r.CommandType?.FullName).OfType<string>().Append(typeof(ChallengeAnswer).FullName).ToHashSet(StringComparer.Ordinal);
        var routes = rows.Select(r => r.Route).OfType<string>().SelectMany(r => new[] { r, r.Replace("/sindri", string.Empty, StringComparison.Ordinal) }).ToHashSet(StringComparer.Ordinal);

        var found = Production.Architecture.Types
            .Where(t => IsClient(t.Assembly.Name) || InMcp(t.Namespace.FullName))
            .SelectMany(t => t.Dependencies.Where(d => types.Contains(d.Target.FullName)).Select(d => $"{t.FullName} uses {d.Target.FullName}"))
            .Concat(Production.Assemblies.Where(a => IsClient(a.GetName().Name ?? string.Empty)).SelectMany(a => Strings(a).Where(routes.Contains).Select(s => $"{a.GetName().Name} holds route '{s}'")))
            .ToList();

        Assert.True(found.Count == 0, string.Join(Environment.NewLine, found)
            + ". Fix: a protected action is typed at a terminal; the app shows the CLI form to copy (engine spec §4.2, UI spec E-9)");
    }

    /// <summary>The Avalonia app (SindriUi, spec §4a 'UI') and any MCP server: clients that never sit at a terminal.</summary>
    private static bool IsClient(string assembly) =>
        assembly.StartsWith("Aw.Sindri.Ui", StringComparison.Ordinal) || assembly.Contains(".Mcp", StringComparison.Ordinal);

    /// <summary>
    /// The Sindri host's MCP namespace: its types depend on no protected request. The host holds every route, so the
    /// route-string half of this rule can't read its whole assembly; McpSurfaceTests reads the MCP methods' literals.
    /// </summary>
    private static bool InMcp(string ns) =>
        ns == McpSurfaceTests.McpNamespace || ns.StartsWith(McpSurfaceTests.McpNamespace + ".", StringComparison.Ordinal);

    private static bool Redeems(Type request) =>
        request.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(p => p.PropertyType == typeof(ChallengeAnswer));

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
