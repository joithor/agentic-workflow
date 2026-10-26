using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri workflow answer &lt;address&gt; --record &lt;batch.json&gt; [--nonce &lt;n&gt;] [--json]' (#1270): hands the interview desk an answer batch without the desk
/// form. It reads the instance, finds the open run of 'step.interview.answer' through <see cref="AnswerRecording"/> (any other state is refused with
/// "rewind to interview first"), and reports that run 'answered' with the file as 'fact:answers' through the same 'complete' route the desk uses, so
/// 'step.interview.record' runs as normal. A batch may carry 'supersedes' (earlier answer ids) and must carry 'recordedAs' naming ids the facts hold,
/// or an item, surface or open question must cite its id, or SCP-ACC-003 fails on it like on any answer.
/// </summary>
internal static class RecordAnswerCommand
{
    private const string Usage = "usage: sindri workflow answer <address> --record <batch.json> [--nonce <n>] [--json] (the batch: a JSON array of answer objects with 'answers', optional 'supersedes', and 'recordedAs' naming ids the facts hold)";

    /// <summary>What the command needs from the verb table.</summary>
    /// <param name="Desk">The API client.</param>
    /// <param name="Services">The scope the clock resolves from.</param>
    /// <param name="Output">Where the verb writes.</param>
    internal sealed record Host(RecipeDesk Desk, IServiceProvider Services, Output Output);

    /// <summary>Records the batch; exit 0 applied, 1 not running, 2 invalid, refused or error.</summary>
    internal static async Task<int> Run(string address, string[] options, bool json, Host host, CancellationToken ct)
    {
        var parsed = Parse(options);
        if (parsed.IsError)
        {
            return await host.Output.Error(parsed.Error).ConfigureAwait(false);
        }

        var (file, nonce) = parsed.Value;
        var id = await InstanceReads.IdOf(host.Desk, address, ct).ConfigureAwait(false);
        var read = id.IsError ? id.Error : await host.Desk.Client.Get<InstanceDetail>($"{InstanceReads.Instances}/{InstanceRoute.Segment(id.Value.Value)}", ct).ConfigureAwait(false);
        if (read.IsError)
        {
            return await host.Output.Error(read.Error).ConfigureAwait(false);
        }

        var run = AnswerRecording.InterviewRun(read.Value.Value);
        if (run.IsError)
        {
            return await host.Output.Error(run.Error).ConfigureAwait(false);
        }

        string[] complete = [address, "--run", run.Value.Value, "--outcome", "answered", "--output", $"fact:answers={file}", .. nonce is null ? [] : new[] { "--nonce", nonce }, .. json ? new[] { "--json" } : []];
        return await CompleteCommand.Run(complete, host.Desk, host.Services, host.Output, ct).ConfigureAwait(false);
    }

    private static Result<(string File, string? Nonce)> Parse(string[] options)
    {
        string? file = null;
        string? nonce = null;
        for (var i = 0; i < options.Length; i += 2)
        {
            if (i + 1 >= options.Length)
            {
                return SindriErrors.InvalidInput.With($"'{options[i]}' needs a value ({Usage})");
            }

            switch (options[i])
            {
                case "--record":
                    file = options[i + 1];
                    break;
                case "--nonce" when CommandNonce.TryFrom(options[i + 1], out _):
                    nonce = options[i + 1];
                    break;
                default:
                    return SindriErrors.InvalidInput.With($"'{options[i]} {options[i + 1]}' isn't an option next to --record ({Usage})");
            }
        }

        return file is null ? SindriErrors.InvalidInput.With(Usage) : (file, nonce);
    }
}
