using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The contract every <see cref="ISingleInstance"/> passes: the fake, and the desktop adapter over a temp directory. A
/// binding's <see cref="Launch"/> stands for one more launch of the app by the same user (the same channel). A send is
/// acknowledged only once the running instance has handled the message (its reader came back for the next one), never
/// merely because a lock is held: a later launch must not report success to a wedged instance.
/// </summary>
public abstract class SingleInstanceContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>One more launch of the app by the same user.</summary>
    /// <returns>Its instance port.</returns>
    protected abstract ISingleInstance Launch();

    /// <summary>Moves the binding's clock on (the adapter's 2 s answer timeout); a binding with no clock does nothing.</summary>
    /// <param name="by">How far.</param>
    protected virtual void Elapse(TimeSpan by)
    {
    }

    // Sends, moving the clock on while the send waits, so a timeout on the binding's clock can fire.
    private Task<bool> SendWhileTimePasses(ISingleInstance sender, string message) =>
        WhileTimePasses(sender.Send(message, Token).AsTask());

    // Moves the clock on while a send waits; a send that never ends fails the test after 5 s rather than hanging it.
    private async Task<bool> WhileTimePasses(Task<bool> sending)
    {
        for (var i = 0; i < 500 && !sending.IsCompleted; i++)
        {
            Elapse(TimeSpan.FromMilliseconds(250));
            await Task.Delay(10, Token);
        }

        return await sending.WaitAsync(TimeSpan.FromSeconds(5), Token);
    }

    // Review run 4 (pattern-recognition-specialist-0): withdrawal is part of the port. A message the reader took but
    // didn't act on before the sender's wait ended is withdrawn: it can't be acted on, and the send isn't acknowledged.
    [Fact]
    public async Task A_message_held_past_the_senders_wait_is_withdrawn_and_not_acknowledged()
    {
        await using var first = Launch();
        await using var second = Launch();
        Expect.True(await first.TryClaim(Token));
        await using var messages = first.Messages(Token).GetAsyncEnumerator(Token);
        var next = messages.MoveNextAsync().AsTask();
        var sending = second.Send("item/ITM-HELD", Token).AsTask();
        Expect.True(await next);
        var message = messages.Current;
        Expect.True(!message.Withdrawn.IsCancellationRequested);

        Expect.True(!await WhileTimePasses(sending));
        Expect.True(message.Withdrawn.IsCancellationRequested);
        Expect.True(!message.TryAct());
    }

    // Review run 4 (julik-frontend-races-reviewer-1, pattern-recognition-specialist-0): acting on a message is one
    // decision. The first TryAct wins and the sender is told the message was handled; once the send has returned, the
    // message is withdrawn and can't be acted on again.
    [Fact]
    public async Task A_message_acted_on_is_acknowledged_and_then_withdrawn()
    {
        await using var first = Launch();
        await using var second = Launch();
        Expect.True(await first.TryClaim(Token));
        await using var messages = first.Messages(Token).GetAsyncEnumerator(Token);
        var next = messages.MoveNextAsync().AsTask();
        var sending = second.Send("item/ITM-ACTED", Token).AsTask();
        Expect.True(await next);
        var message = messages.Current;

        Expect.True(message.TryAct());
        Expect.True(await sending.WaitAsync(TimeSpan.FromSeconds(5), Token));
        Expect.True(message.Withdrawn.IsCancellationRequested);
        Expect.True(!message.TryAct());
    }

    // The reader coming back for the next message handles this one too (the app took it without acting on the UI
    // thread): the send is acknowledged, and then the message is withdrawn.
    [Fact]
    public async Task A_message_the_reader_came_back_from_is_acknowledged_and_then_withdrawn()
    {
        await using var first = Launch();
        await using var second = Launch();
        Expect.True(await first.TryClaim(Token));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var messages = first.Messages(stop.Token).GetAsyncEnumerator(stop.Token);
        var after = Task.FromResult(false);
        try
        {
            var next = messages.MoveNextAsync().AsTask();
            var sending = second.Send("item/ITM-READ", Token).AsTask();
            Expect.True(await next);
            var message = messages.Current;
            after = messages.MoveNextAsync().AsTask();

            Expect.True(await sending.WaitAsync(TimeSpan.FromSeconds(5), Token));
            Expect.True(message.Withdrawn.IsCancellationRequested);
            Expect.True(!message.TryAct());
        }
        finally
        {
            await stop.CancelAsync();
            try
            {
                _ = await after;
            }
            catch (OperationCanceledException)
            {
                // The reader stopped.
            }

            await messages.DisposeAsync();
        }
    }

    [Fact]
    public async Task The_first_launch_claims_and_a_second_does_not()
    {
        await using var first = Launch();
        await using var second = Launch();

        Expect.True(await first.TryClaim(Token));
        Expect.True(!await second.TryClaim(Token));
    }

    [Fact]
    public async Task A_second_launchs_messages_reach_the_first_in_order()
    {
        await using var first = Launch();
        await using var second = Launch();
        Expect.True(await first.TryClaim(Token));
        await using var reader = new InstanceReader(first, Token);

        Expect.True(await second.Send(InstanceMessages.Focus, Token));
        Expect.True(await second.Send("item/ITM-01JC8", Token));

        Expect.Sequence([InstanceMessages.Focus, "item/ITM-01JC8"], await reader.Take(2));
    }

    // The instance holds the claim but nothing iterates its Messages (the app's reader never started: a framework start-up
    // that failed or hangs, or one that stopped reading): the send isn't acknowledged, and the message isn't handed over
    // later either. Once a reader runs, the next send is handled. The clock moves only while the unread send waits; the
    // handled send runs with the clock still, so no timeout can race the reader.
    [Fact]
    public async Task A_message_the_running_instance_never_reads_is_not_acknowledged_or_delivered_later()
    {
        await using var first = Launch();
        await using var second = Launch();
        Expect.True(await first.TryClaim(Token));

        Expect.True(!await SendWhileTimePasses(second, "item/ITM-STALE"));

        await using var reader = new InstanceReader(first, Token);
        Expect.True(await second.Send(InstanceMessages.Focus, Token));
        Expect.Sequence([InstanceMessages.Focus], await reader.Take(1));
    }

    [Fact]
    public async Task A_later_launch_sees_the_running_instances_process_id_and_none_once_it_ends()
    {
        var first = Launch();
        await using var second = Launch();
        Expect.True(second.HolderProcessId() is null);

        Expect.True(await first.TryClaim(Token));
        Expect.True(second.HolderProcessId() is > 0);

        await first.DisposeAsync();
        Expect.True(second.HolderProcessId() is null);
    }

    [Fact]
    public async Task Sending_with_no_running_instance_reaches_nobody()
    {
        await using var lone = Launch();

        Expect.True(!await lone.Send(InstanceMessages.Focus, Token));
    }

    [Fact]
    public async Task A_message_over_the_limit_is_refused_and_the_next_one_still_arrives()
    {
        await using var first = Launch();
        await using var second = Launch();
        Expect.True(await first.TryClaim(Token));
        await using var reader = new InstanceReader(first, Token);

        Expect.True(!await second.Send(new string('x', InstanceMessages.MaxLength + 1), Token));
        Expect.True(await second.Send(InstanceMessages.Focus, Token));

        Expect.Sequence([InstanceMessages.Focus], await reader.Take(1));
    }

    [Fact]
    public async Task Once_the_running_instance_ends_a_new_launch_claims()
    {
        var first = Launch();
        Expect.True(await first.TryClaim(Token));
        await first.DisposeAsync();
        await using var next = Launch();

        Expect.True(await next.TryClaim(Token));
    }

    [Fact]
    public async Task A_launch_that_holds_no_claim_hears_no_messages()
    {
        await using var first = Launch();
        await using var second = Launch();
        Expect.True(await first.TryClaim(Token));
        Expect.True(!await second.TryClaim(Token));

        Expect.Sequence([], await second.Messages(Token).Select(m => m.Text).ToListAsync(Token));
    }
}
