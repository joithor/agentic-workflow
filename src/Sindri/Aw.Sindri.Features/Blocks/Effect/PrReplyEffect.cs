using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.reply' (PB spec §5, §10.1): the 'thread' form posts 'body' on the comment's thread, the 'reaction' form reacts to it, both once per effect
/// identity (Sindri's reply marker is derived from it, and the forge says when it was already delivered). The 'fixed', 'fixedReplies', 'hold' and 'none'
/// forms post nothing; their marks (W26's table) are not stored yet, so they only complete. The recipe's transitions don't wait on a reply, so it replies nothing.
/// </summary>
/// <param name="pulls">The pull requests port.</param>
/// <param name="facts">Reads 'fact:pr', 'comment' and 'body' back.</param>
internal sealed class PrReplyEffect(IPullRequests pulls, IFactContents facts) : IEffectKind, IDescribedBlock
{
    private const int MarkerLimit = 128;

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.Reply;

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var form = context.Arguments.TryGetValue("form", out var named) ? PrEffectFacts.Text(named) : null;
        return form switch
        {
            "thread" => await Post(context, reaction: false, cancellationToken).ConfigureAwait(false),
            "reaction" => await Post(context, reaction: true, cancellationToken).ConfigureAwait(false),
            "fixed" or "fixedReplies" or "hold" or "none" => new EffectDone(Reply: null, Existed: false),
            _ => WorkflowErrors.NoRuntime.With($"effect.pr.reply of {context.Instance} has no form '{form}' (thread, reaction, fixed, fixedReplies, none or hold)"),
        };
    }

    private async ValueTask<Result<EffectDone>> Post(EffectContext context, bool reaction, CancellationToken cancellationToken)
    {
        var pr = await PrEffectFacts.Fact(facts, context, "fact:pr", cancellationToken).ConfigureAwait(false);
        var comment = await PrEffectFacts.Argument(facts, context, "comment", cancellationToken).ConfigureAwait(false);
        var body = reaction ? default : await PrEffectFacts.Argument(facts, context, "body", cancellationToken).ConfigureAwait(false);
        if (pr.IsError || comment.IsError || body.IsError)
        {
            return pr.IsError ? pr.Error : comment.IsError ? comment.Error : body.Error;
        }

        var link = PrEffectFacts.Text(pr.Value, "link", "url", "pr");
        var thread = PrEffectFacts.Text(comment.Value, "threadId", "thread", "id");
        var text = reaction ? "+1" : PrEffectFacts.Text(body.Value, "body", "text", "reply");
        if (string.IsNullOrEmpty(link) || string.IsNullOrEmpty(thread) || string.IsNullOrWhiteSpace(text))
        {
            return WorkflowErrors.NoRuntime.With($"effect.pr.reply of {context.Instance} needs the pull request ('fact:pr'), the comment's thread and, for a thread reply, its body");
        }

        var marker = Marker(context.Identity);
        var delivered = reaction
            ? await pulls.React(new PullReaction(link, thread, text, marker), cancellationToken).ConfigureAwait(false)
            : await pulls.Reply(new PullReply(link, thread, text, marker), cancellationToken).ConfigureAwait(false);
        return delivered.IsError ? delivered.Error : new EffectDone(Reply: null, Existed: delivered.Value == PullDelivery.AlreadyDelivered);
    }

    /// <summary>Sindri's reply marker for an effect identity: the forge allows letters, digits, '.', '_', ':' and '-' (128 at most), so anything else becomes '-'.</summary>
    private static string Marker(string identity)
    {
        var marker = new StringBuilder("sindri:");
        foreach (var c in identity)
        {
            _ = marker.Append(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or ':' or '-' ? c : '-');
        }

        return marker.Length > MarkerLimit ? marker.ToString(0, MarkerLimit) : marker.ToString();
    }
}
