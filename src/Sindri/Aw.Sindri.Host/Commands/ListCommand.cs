using System.Text;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Features.ListInstances;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri workflow list [--needs-me | --item &lt;id&gt; | --state &lt;s&gt;] [--query &lt;visibility query&gt;] [--json]'
/// (engine spec §6.3; engine P4 unit (e)): one status header per line. A client of serve's 'GET /workflow/instances' (review run 2); it never starts serve. '--needs-me' shows <b>Decisions</b> and then
/// <b>Tasks</b>, each under its heading; empty, it prints 'nothing needs you', and the other filters 'no instances match'.
/// Exit 0, empty included; 1 when Sindri isn't running; 2 on any other error.
/// </summary>
internal static class ListCommand
{
    private const string Usage = "usage: sindri workflow list [--needs-me | --item <id> | --state <s>] [--query <visibility query>] [--json]";

    internal static async Task<int> Run(string[] args, RecipeDesk desk, IServiceProvider services, Output output, CancellationToken ct)
    {
        var json = args.Contains("--json", StringComparer.Ordinal);
        var request = Parse([.. args.Where(static a => a != "--json")]);
        if (request is null)
        {
            return await output.Error(SindriErrors.InvalidInput.With(Usage)).ConfigureAwait(false);
        }

        var listed = await desk.Client.Get<ListInstancesResponse>(Route(request), ct).ConfigureAwait(false);
        if (listed.IsError)
        {
            return await output.Error(InstanceReads.NotRunning(listed.Error)).ConfigureAwait(false);
        }

        return json
            ? await output.Json(listed.Value.Value, 0).ConfigureAwait(false)
            : await output.Text(Text(listed.Value.Value, services.GetRequiredService<TimeProvider>().GetUtcNow()), 0).ConfigureAwait(false);
    }

    /// <summary>'GET /workflow/instances' with each filter given as its query parameter.</summary>
    private static string Route(ListInstancesRequest request)
    {
        var parts = new List<string>(4);
        if (request.NeedsMe)
        {
            parts.Add("needsMe=true");
        }

        if (request.Item is { } item)
        {
            parts.Add("item=" + Uri.EscapeDataString(item.Value));
        }

        if (request.State is { } state)
        {
            parts.Add("state=" + Uri.EscapeDataString(state));
        }

        if (request.Query is { } query)
        {
            parts.Add("query=" + Uri.EscapeDataString(query));
        }

        return parts.Count == 0 ? InstanceReads.Instances : $"{InstanceReads.Instances}?{string.Join('&', parts)}";
    }

    /// <summary>The flags, each at most once, '--needs-me' alone of the filters it excludes; null when they don't parse.</summary>
    private static ListInstancesRequest? Parse(string[] args)
    {
        var request = new ListInstancesRequest();
        for (var i = 0; i < args.Length; i++)
        {
            var (applied, used) = Apply(request, args[i], i + 1 < args.Length ? args[i + 1] : null);
            if (applied is null)
            {
                return null;
            }

            request = applied;
            i += used ? 1 : 0;
        }

        return request.NeedsMe && (request.Item is not null || request.State is not null) ? null : request;
    }

    /// <summary>One flag applied: the request after it (null when it doesn't parse) and whether it took the next argument.</summary>
    private static (ListInstancesRequest? Request, bool UsedValue) Apply(ListInstancesRequest request, string flag, string? value) => flag switch
    {
        "--needs-me" when !request.NeedsMe => (request with { NeedsMe = true }, false),
        "--item" when request.Item is null && value is not null && ItemKey.TryFrom(value, out var key) => (request with { Item = key }, true),
        "--state" when request.State is null && value is not null => (request with { State = value }, true),
        "--query" when request.Query is null && value is not null => (request with { Query = value }, true),
        _ => (null, false),
    };

    private static string Text(ListInstancesResponse listed, DateTimeOffset now)
    {
        if (listed.Empty is { } empty)
        {
            return empty + "\n";
        }

        var text = new StringBuilder();
        if (listed.NeedsYou is { } sections)
        {
            Section(text, "Decisions", sections.Decisions, now);
            Section(text, "Tasks", sections.Tasks, now);
            return text.ToString();
        }

        foreach (var view in listed.Instances)
        {
            _ = text.Append(InstanceHeader.Line(view, now)).Append('\n');
        }

        return text.ToString();
    }

    private static void Section(StringBuilder text, string heading, IReadOnlyList<InstanceView> views, DateTimeOffset now)
    {
        if (views.Count == 0)
        {
            return;
        }

        _ = text.Append(heading).Append('\n');
        foreach (var view in views)
        {
            _ = text.Append("  ").Append(InstanceHeader.Line(view, now)).Append('\n');
        }
    }
}
