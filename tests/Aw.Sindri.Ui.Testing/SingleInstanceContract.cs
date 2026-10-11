using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The contract every <see cref="ISingleInstance"/> passes: the fake, and the desktop adapter over a temp directory. A
/// binding's <see cref="Launch"/> stands for one more launch of the app by the same user (the same channel).
/// </summary>
public abstract class SingleInstanceContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>One more launch of the app by the same user.</summary>
    /// <returns>Its instance port.</returns>
    protected abstract ISingleInstance Launch();

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

        Expect.True(await second.Send(InstanceMessages.Focus, Token));
        Expect.True(await second.Send("item/ITM-01JC8", Token));

        Expect.Sequence([InstanceMessages.Focus, "item/ITM-01JC8"], await Take(first, 2));
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

        Expect.True(!await second.Send(new string('x', InstanceMessages.MaxLength + 1), Token));
        Expect.True(await second.Send(InstanceMessages.Focus, Token));

        Expect.Sequence([InstanceMessages.Focus], await Take(first, 1));
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

        Expect.Sequence([], await second.Messages(Token).ToListAsync(Token));
    }

    private static async Task<List<string>> Take(ISingleInstance instance, int count)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var seen = new List<string>();
        try
        {
            await foreach (var message in instance.Messages(deadline.Token))
            {
                seen.Add(message);
                if (seen.Count == count)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (!Token.IsCancellationRequested)
        {
            // Fewer than count arrived within 5 s; the caller's check names what did.
        }

        return seen;
    }
}
