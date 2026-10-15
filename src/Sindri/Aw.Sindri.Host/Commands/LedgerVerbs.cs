using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Features.ReadLedger;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri ledger [--after N] [--item X] [--type T] [--max N] [--json] [--no-start]' (API P5 unit F): a client of GET /sindri/ledger.
/// A human verb: it starts the control plane unless --no-start, and inside an agent session it never does (API spec §7). The text form is one
/// line per event, then 'next: N' while the ledger has more; --json is the raw body.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class LedgerVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    internal const string Usage = "ledger [--after N] [--item X] [--type T] [--max N] [--json] [--no-start]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var parsed = Parse(args);
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var down = ensureRunning is null ? null : await ensureRunning(!parsed.Value.NoStart, ct).ConfigureAwait(false);
        if (down is not null)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var answer = await client.Get<LedgerPage>("/sindri/ledger" + parsed.Value.Query, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        return parsed.Value.Json
            ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(Render(answer.Value.Value), 0).ConfigureAwait(false);
    }

    private static string Render(LedgerPage page)
    {
        var text = new StringBuilder();
        foreach (var e in page.Events)
        {
            var item = e.Item is { } id ? "  " + id.Value : string.Empty;
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{e.Sequence}  {e.OccurredAt.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}  {e.Type}  {e.Actor}{item}"));
        }

        if (page.Next is { } next)
        {
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"next: {next}"));
        }

        return text.ToString();
    }

    private static Result<Arguments> Parse(string[] args)
    {
        var query = new List<string>();
        bool json = false, noStart = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--no-start":
                    noStart = true;
                    break;
                case "--after" or "--max" when i + 1 < args.Length:
                    if (!long.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    {
                        return SindriErrors.InvalidInput.With($"{args[i]} needs a whole number, not '{args[i + 1]}': {Usage}");
                    }

                    query.Add(args[i][2..] + "=" + args[++i]);
                    break;
                case "--item" or "--type" when i + 1 < args.Length:
                    query.Add(args[i][2..] + "=" + Uri.EscapeDataString(args[++i]));
                    break;
                default:
                    return SindriErrors.InvalidInput.With($"'{args[i]}' isn't an argument of this command (or it needs a value): {Usage}");
            }
        }

        return new Arguments(query.Count == 0 ? string.Empty : "?" + string.Join('&', query), json, noStart);
    }

    private sealed record Arguments(string Query, bool Json, bool NoStart);
}
