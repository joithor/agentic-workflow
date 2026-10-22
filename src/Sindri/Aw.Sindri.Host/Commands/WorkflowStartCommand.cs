using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.StartInstance;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri workflow start &lt;recipe&gt; --item &lt;id&gt; [--attempt next] [--fact &lt;key&gt;=&lt;file&gt;]… [--json]'
/// (engine spec rev 8 §5.1, §6.1, §6.3; engine P4 unit (b)). The CLI reads each fact file (at most 1 MiB) and sends its
/// name and content, never its path, to the API's StartInstance route. It prints the status header; an attempt that has ended
/// adds 'start a new one with --attempt next'. Exit 0 started or found, 1 not running, 2 invalid, refused (SND-WF-012 …) or error.
/// </summary>
internal static class WorkflowStartCommand
{
    private const string Usage = "usage: sindri workflow start <recipe> --item <id> [--attempt next] [--fact <key>=<file>]... [--json]";

    internal static async Task<int> Run(string[] args, RecipeDesk desk, IServiceProvider services, Output output, CancellationToken ct)
    {
        var json = args.Contains("--json", StringComparer.Ordinal);
        var request = await Parse([.. args.Where(static a => a != "--json")], ct).ConfigureAwait(false);
        if (request.IsError)
        {
            return await output.Error(request.Error).ConfigureAwait(false);
        }

        return await Start(request.Value, json, (desk, services, output), ct).ConfigureAwait(false);
    }

    /// <summary>Starts the instance through the running control plane and prints it: the status header, or with <paramref name="json"/> the raw answer.</summary>
    /// <param name="request">The start.</param>
    /// <param name="json">Whether to print the raw answer.</param>
    /// <param name="wire">The desk, the services and the output the verb runs with.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The exit code: 0 started or found, 1 not running, 2 refused or error.</returns>
    internal static async Task<int> Start(StartInstanceRequest request, bool json, (RecipeDesk Desk, IServiceProvider Services, Output Output) wire, CancellationToken ct)
    {
        var (desk, services, output) = wire;
        if (await desk.EnsureRunning(ct).ConfigureAwait(false) is { } down)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var started = await desk.Client.Post<StartInstanceRequest, StartInstanceResponse>("/sindri/workflow/instances", request, ct).ConfigureAwait(false);
        if (started.IsError)
        {
            return await output.Error(started.Error).ConfigureAwait(false);
        }

        if (json)
        {
            return await output.Raw(started.Value.Body, 0).ConfigureAwait(false);
        }

        var answer = started.Value.Value;
        var header = InstanceHeader.Line(answer.Instance, services.GetRequiredService<TimeProvider>().GetUtcNow());
        return await output.Text(header + "\n" + (answer.Finished ? "  start a new one with --attempt next\n" : string.Empty), 0).ConfigureAwait(false);
    }

    private static async Task<Result<StartInstanceRequest>> Parse(string[] args, CancellationToken ct)
    {
        if (args is not [var recipe, .. var pairs] || recipe.StartsWith("--", StringComparison.Ordinal))
        {
            return SindriErrors.InvalidInput.With($"the first argument is the recipe id ({Usage})");
        }

        if (pairs.Length % 2 != 0)
        {
            return SindriErrors.InvalidInput.With($"'{pairs[^1]}' needs a value ({Usage})");
        }

        var flags = pairs.Chunk(2).Select(static p => (Flag: p[0], Value: p[1])).ToList();
        if (flags.FirstOrDefault(static f => f.Flag is not ("--item" or "--attempt" or "--fact")) is { Flag: { } unknown })
        {
            return SindriErrors.InvalidInput.With($"'{unknown}' isn't an option ({Usage})");
        }

        if (flags.LastOrDefault(static f => f.Flag == "--item").Value is not { Length: > 0 } item)
        {
            return SindriErrors.InvalidInput.With($"--item <id> is required ({Usage})");
        }

        if (flags.LastOrDefault(static f => f.Flag == "--attempt").Value is { } attempt && attempt != "next")
        {
            return SindriErrors.InvalidInput.With($"--attempt takes only 'next' ({Usage})");
        }

        var facts = new Dictionary<FactKey, StartFact>();
        foreach (var value in flags.Where(static f => f.Flag == "--fact").Select(static f => f.Value))
        {
            var read = await Fact(value, ct).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            facts[read.Value.Key] = read.Value.Fact;
        }

        return new StartInstanceRequest(recipe, item, flags.Exists(static f => f.Flag == "--attempt"), facts);
    }

    /// <summary>Reads '&lt;key&gt;=&lt;file&gt;': the file's name and content, at most 1 MiB (spec §6.1); the server never sees the path.</summary>
    private static async Task<Result<(FactKey Key, StartFact Fact)>> Fact(string value, CancellationToken ct)
    {
        if (value.Split('=', 2) is not [var fact, { Length: > 0 } file] || !FactKey.TryFrom(fact, out var key))
        {
            return SindriErrors.InvalidInput.With($"'{value}' isn't <key>=<file>, with a fact key such as fact:spec ({Usage})");
        }

        try
        {
            var info = new FileInfo(file);
            if (info.Exists && info.Length > IFactStore.MaxBytes)
            {
                return SindriErrors.InvalidInput.With($"{file} is larger than 1 MiB (fix: start it with a smaller file; facts travel as content, at most 1 MiB each)");
            }

            return (key, new StartFact(info.Name, await File.ReadAllBytesAsync(file, ct).ConfigureAwait(false)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) // boundary: an unreadable file is the caller's input error
        {
            return SindriErrors.InvalidInput.With($"{file} can't be read: {e.Message}");
        }
    }
}
