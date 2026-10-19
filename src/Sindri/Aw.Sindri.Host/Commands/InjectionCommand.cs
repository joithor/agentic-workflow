using System.Text;
using Aw.Sindri.Core;
using Aw.Sindri.Features.ReadInjection;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri workflow injection &lt;instance&gt; [--section S] [--json]': pulls the text behind the open run's injection plan from the control
/// plane (the plan lists sections by name and size only). Text prints each section under a heading line; --json prints the response.
/// </summary>
internal static class InjectionCommand
{
    private const string Usage = "usage: sindri workflow injection <instance> [--section <name>] [--json]";

    internal static async Task<int> Run(string[] args, RecipeDesk desk, Output output, CancellationToken ct)
    {
        if (Parse(args) is not var (text, section))
        {
            return await output.Error(SindriErrors.InvalidInput.With($"the one argument is the instance: its address (e.g. T-1/step/1) or a bare item ({Usage})")).ConfigureAwait(false);
        }

        var id = await InstanceReads.IdOf(desk, text, ct).ConfigureAwait(false);
        var route = $"{InstanceReads.Instances}/{(id.IsError ? string.Empty : InstanceRoute.Segment(id.Value.Value))}/injection" + (section is null ? string.Empty : "?section=" + Uri.EscapeDataString(section));
        var got = id.IsError ? id.Error : await desk.Client.Get<InjectionView>(route, ct).ConfigureAwait(false);
        if (got.IsError)
        {
            return await output.Error(InstanceReads.NotRunning(got.Error)).ConfigureAwait(false);
        }

        return args.Contains("--json", StringComparer.Ordinal) ? await output.Json(got.Value.Value, 0).ConfigureAwait(false) : await output.Text(Lines(got.Value.Value), 0).ConfigureAwait(false);
    }

    private static (string Instance, string? Section)? Parse(string[] args)
    {
        var section = args.SkipWhile(static a => a != "--section").Skip(1).FirstOrDefault();
        var rest = args.Where(a => a is not "--json" and not "--section" && !string.Equals(a, section, StringComparison.Ordinal)).ToArray();
        var sectionMissing = args.Contains("--section", StringComparer.Ordinal) && section is null;
        return rest is [{ Length: > 0 } text] && !text.StartsWith("--", StringComparison.Ordinal) && !sectionMissing ? (text, section) : null;
    }

    internal static string Lines(InjectionView view)
    {
        if (view.Sections.Count == 0)
        {
            return "no injection\n";
        }

        var text = new StringBuilder();
        foreach (var s in view.Sections)
        {
            _ = text.Append("== ").Append(s.Section).Append(" (").Append(s.Input).Append(", ").Append(s.Bytes).Append(" bytes)\n").Append(s.Text).Append(s.Text.EndsWith('\n') ? string.Empty : "\n");
        }

        return text.ToString();
    }
}
