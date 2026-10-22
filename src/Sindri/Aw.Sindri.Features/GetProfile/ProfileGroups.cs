using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.GetProfile;

/// <summary>
/// The wording of the profile preview's allowance, connections and human-only groups (setup-once spec §5.2 "Preview"): what the live profile
/// adds or widens against the approved one, and every explicit setting when nothing is approved. Pure; a key the profile leaves out says nothing.
/// </summary>
internal static class ProfileGroups
{
    private const int DefaultParallel = 2;
    private const int DefaultOpenPrs = 4;
    private const int DefaultStack = 4;
    private const int DefaultAutofixCap = 3;

    /// <summary>The spending and work limits the live profile raises or adds.</summary>
    /// <param name="approved">The approved profile, or null.</param>
    /// <param name="live">The live profile.</param>
    /// <returns>One sentence per limit, in a fixed order.</returns>
    public static IEnumerable<string> Allowance(SindriProfile? approved, SindriProfile live)
    {
        foreach (var sentence in DailyBudgets(approved?.Agents, live.Agents))
        {
            yield return sentence;
        }

        var was = approved?.Workflow;
        var now = live.Workflow;
        var fresh = approved is null;
        if (Widened(was?.Line?.Parallel, now?.Line?.Parallel, DefaultParallel, fresh) is { } parallel)
        {
            yield return Number($"Sindri may run up to {parallel} phases at once");
        }

        if (Widened(was?.Line?.MaxOpenPrs, now?.Line?.MaxOpenPrs, DefaultOpenPrs, fresh) is { } open)
        {
            yield return Number($"Sindri may keep up to {open} pull requests open");
        }

        if (Widened(was?.Line?.MaxStack, now?.Line?.MaxStack, DefaultStack, fresh) is { } stack)
        {
            yield return Number($"Sindri may stack phases up to {stack} deep");
        }

        if (Widened(was?.Pr?.AutofixCap, now?.Pr?.AutofixCap, DefaultAutofixCap, fresh) is { } fixes)
        {
            yield return Number($"Sindri may push up to {fixes} CI fixes for one pull request");
        }

        foreach (var sentence in SizeBudgets(was?.Plan, now?.Plan, fresh))
        {
            yield return sentence;
        }
    }

    /// <summary>The outside systems the live profile points Sindri at that the approved one did not.</summary>
    /// <param name="approved">The approved profile, or null.</param>
    /// <param name="live">The live profile.</param>
    /// <returns>One sentence per connection, in a fixed order.</returns>
    public static IEnumerable<string> Connections(SindriProfile? approved, SindriProfile live)
    {
        if (OnyxSentences(approved, live) is { } onyx)
        {
            foreach (var sentence in onyx)
            {
                yield return sentence;
            }
        }

        if (ForgeSentence(approved?.Workflow?.Forge, live.Workflow?.Forge) is { } forge)
        {
            yield return forge;
        }

        foreach (var sentence in Providers(approved, live))
        {
            yield return sentence;
        }

        if (live.Workflow?.Agent?.Target is { } target && !string.Equals(target.Value, approved?.Workflow?.Agent?.Target?.Value, StringComparison.Ordinal))
        {
            yield return $"Workflow steps will launch the {target.Value} agent";
        }

        if (live.Launch?.Default is { } launch && !string.Equals(launch.Value, approved?.Launch?.Default?.Value, StringComparison.Ordinal))
        {
            yield return $"Sindri will launch the {launch.Value} agent by default";
        }

        if (string.Equals(live.Setup?.Mcp, SetupSettings.Auto, StringComparison.Ordinal) && !string.Equals(approved?.Setup?.Mcp, SetupSettings.Auto, StringComparison.Ordinal))
        {
            yield return "Sindri will register its MCP server in each repo on its own";
        }
    }

    /// <summary>The steps the live profile keeps for you alone that the approved one did not.</summary>
    /// <param name="approved">The approved profile, or null.</param>
    /// <param name="live">The live profile.</param>
    /// <returns>One sentence per stop, in a fixed order.</returns>
    public static IEnumerable<string> HumanOnly(SindriProfile? approved, SindriProfile live)
    {
        var was = approved?.Workflow;
        var now = live.Workflow;
        if (string.Equals(now?.Pr?.Replies, "desk", StringComparison.Ordinal) && !string.Equals(was?.Pr?.Replies, "desk", StringComparison.Ordinal))
        {
            yield return "You send every reply to a pull request comment yourself";
        }

        if (now?.Pr?.HeldBlocksMerge == true && was?.Pr?.HeldBlocksMerge != true)
        {
            yield return "A held comment stops the merge until you clear it";
        }

        if (string.Equals(now?.Route?.Startable, RouteSettings.None, StringComparison.Ordinal) && !string.Equals(was?.Route?.Startable, RouteSettings.None, StringComparison.Ordinal))
        {
            yield return "Routed items stop at the router until you start them";
        }
    }

    private static IEnumerable<string> DailyBudgets(AgentSettings? approved, AgentSettings? live)
    {
        var before = approved?.Targets ?? new Dictionary<string, AgentLimits>(StringComparer.Ordinal);
        var after = live?.Targets ?? new Dictionary<string, AgentLimits>(StringComparer.Ordinal);
        foreach (var (agent, limits) in after.OrderBy(static t => t.Key, StringComparer.Ordinal))
        {
            var was = before.TryGetValue(agent, out var old) ? old.DailyBudget : null;
            if (limits.DailyBudget is { } budget && (was is null || budget > was))
            {
                yield return Number($"{agent} may spend up to ${budget:0.##} a day");
            }
            else if (limits.DailyBudget is null && was is not null)
            {
                yield return $"{agent} will have no daily spending limit";
            }
        }

        foreach (var agent in before.Where(static t => t.Value.DailyBudget is not null).Select(static t => t.Key).Where(a => !after.ContainsKey(a)).Order(StringComparer.Ordinal))
        {
            yield return $"{agent} will have no daily spending limit";
        }
    }

    private static IEnumerable<string> SizeBudgets(PlanSettings? approved, PlanSettings? live, bool fresh)
    {
        foreach (var (size, settings) in (live?.Sizes ?? new Dictionary<string, SizeSettings>(StringComparer.Ordinal)).OrderBy(static s => s.Key, StringComparer.Ordinal))
        {
            var was = approved?.Sizes is { } sizes && sizes.TryGetValue(size, out var old) ? old.Budget : null;
            List<string> parts = [];
            Add(parts, "tokens", was?.Tokens, settings.Budget?.Tokens, fresh);
            Add(parts, "turns", was?.Turns, settings.Budget?.Turns, fresh);
            Add(parts, "minutes", was?.WallMinutes, settings.Budget?.WallMinutes, fresh);
            if (parts.Count > 0)
            {
                yield return $"A {size} phase may use up to {string.Join(", ", parts)}";
            }
        }
    }

    private static void Add(List<string> parts, string unit, long? was, long? now, bool fresh)
    {
        if (now is { } value && (fresh || was is null || value > was))
        {
            parts.Add(Number($"{value} {unit}"));
        }
    }

    // The value to say when the live key is above the approved one (a missing key is its default); with nothing approved, any key the profile sets.
    private static int? Widened(int? was, int? now, int fallback, bool fresh) =>
        now is { } value && (fresh || value > (was ?? fallback)) ? value : null;

    private static string Number(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static IEnumerable<string>? OnyxSentences(SindriProfile? approved, SindriProfile live)
    {
        var after = Onyx(live);
        if (after is null)
        {
            return null;
        }

        var before = approved is null ? null : Onyx(approved);
        List<string> sentences = [];
        if (after.Value.Url is { } url && !string.Equals(url, before?.Url, StringComparison.Ordinal))
        {
            sentences.Add($"Sindri will send what it reads to your Onyx at {url}");
        }

        if (after.Value.Managed && before?.Managed != true)
        {
            sentences.Add("Sindri will start and look after Onyx on this machine");
        }

        return sentences;
    }

    // 'context.sources.onyx' is the context engine's section: only its url and managed keys are read, and never the token.
    private static (string? Url, bool Managed)? Onyx(SindriProfile profile)
    {
        if (profile.Context is not { ValueKind: JsonValueKind.Object } context
            || !context.TryGetProperty("sources", out var sources) || sources.ValueKind != JsonValueKind.Object
            || !sources.TryGetProperty("onyx", out var onyx) || onyx.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var url = onyx.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
        var managed = onyx.TryGetProperty("managed", out var m) && m.ValueKind == JsonValueKind.True;
        return (url, managed);
    }

    private static string? ForgeSentence(ForgeSettings? approved, ForgeSettings? live)
    {
        if (live?.Adapter is not { } adapter)
        {
            return null;
        }

        var hosts = live.Hosts ?? [];
        var same = string.Equals(approved?.Adapter, adapter, StringComparison.Ordinal)
            && string.Equals(approved?.Credential, live.Credential, StringComparison.Ordinal)
            && hosts.All(h => approved?.Hosts?.Contains(h, StringComparer.Ordinal) == true);
        if (same)
        {
            return null;
        }

        var where = hosts.Count == 0 ? string.Empty : $" at {string.Join(", ", hosts)}";
        return $"Sindri will use the {adapter} forge{where}, with {CredentialWords(live.Credential)}";
    }

    // The source of the credential, never its value: a command is not echoed either.
    private static string CredentialWords(string? credential) => credential switch
    {
        null => "the token in AW_FORGE_TOKEN",
        _ when credential.StartsWith("env:", StringComparison.Ordinal) => $"the token in {credential["env:".Length..]}",
        _ => "a token from a command it runs",
    };

    private static IEnumerable<string> Providers(SindriProfile? approved, SindriProfile live)
    {
        var shown = new HashSet<string>(StringComparer.Ordinal);
        var allowed = new HashSet<string>(approved is null ? [] : JudgeProviders.Effective(approved, repo: null, recipeLiteral: null), StringComparer.Ordinal);
        foreach (var provider in (live.Workflow?.Judge?.Providers ?? (approved is null ? JudgeProviders.Default : [])).Where(p => !allowed.Contains(p)))
        {
            if (shown.Add(provider))
            {
                yield return $"Judge steps may ask {provider}";
            }
        }

        foreach (var repo in live.Repos ?? [])
        {
            foreach (var provider in (repo.Workflow?.Judge?.Providers ?? []).Where(p => !allowed.Contains(p) && !(approved?.Repos?.FirstOrDefault(r => r.Name == repo.Name)?.Workflow?.Judge?.Providers?.Contains(p, StringComparer.Ordinal) ?? false)))
            {
                if (shown.Add(provider))
                {
                    yield return $"Judge steps may ask {provider}";
                }
            }
        }
    }
}
