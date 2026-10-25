using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.CodeIndex.Testing;

/// <summary>
/// What every <see cref="IParseQuarantine"/> honours (spec §9 CI12): a mark left by a run that died quarantines its blob
/// at the next start, an ended mark does not, the list outlives the process, drops blobs that left the head, is capped,
/// and is kept per repo. A derived class binds <see cref="Create"/> and <see cref="Restart"/> to one implementation.
/// </summary>
public abstract class ParseQuarantineContract
{
    private static readonly RepoName _repo = RepoName.From("alpha");
    private static readonly RepoName _other = RepoName.From("beta");

    /// <summary>A quarantine over empty state.</summary>
    /// <returns>The quarantine.</returns>
    protected abstract IParseQuarantine Create();

    /// <summary>A quarantine the way a new process finds it after <paramref name="previous"/> died: the same marks and lists.</summary>
    /// <param name="previous">The instance of the run that died.</param>
    /// <returns>A new instance over the same state.</returns>
    protected abstract IParseQuarantine Restart(IParseQuarantine previous);

    /// <summary>A fresh quarantine is empty and recovers nothing.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public Task A_fresh_quarantine_is_empty()
    {
        var quarantine = Create();

        Require(quarantine.Recover(_repo) == 0, "recovering with no mark leaves the list empty");
        Require(!quarantine.Contains(_repo, Blob(1)), "no blob is quarantined");
        return Task.CompletedTask;
    }

    /// <summary>A mark left by a run that died puts its blob on the list at the next start.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public Task A_mark_left_behind_quarantines_its_blob()
    {
        var died = Create();
        died.Begin(_repo, Blob(1));

        var next = Restart(died);

        Require(next.Recover(_repo) == 1, "the left-behind blob is on the list");
        Require(next.Contains(_repo, Blob(1)), "the blob is quarantined");
        Require(next.Recover(_repo) == 1, "recovering again finds no mark and adds nothing");
        return Task.CompletedTask;
    }

    /// <summary>A mark that was ended quarantines nothing.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public Task An_ended_mark_quarantines_nothing()
    {
        var run = Create();
        run.Begin(_repo, Blob(1));
        run.End(_repo);

        Require(Restart(run).Recover(_repo) == 0, "nothing to recover after End");
        return Task.CompletedTask;
    }

    /// <summary>The list outlives the process and one blob is listed once.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public Task The_list_survives_a_restart_and_holds_a_blob_once()
    {
        var first = Create();
        first.Begin(_repo, Blob(1));
        var second = Restart(first);
        _ = second.Recover(_repo);
        second.Begin(_repo, Blob(1));
        var third = Restart(second);

        Require(third.Recover(_repo) == 1, "the same blob twice is one entry");
        Require(Restart(third).Contains(_repo, Blob(1)), "a later restart still has it");
        return Task.CompletedTask;
    }

    /// <summary>Keep drops the blobs that are no longer in the head, and the drop lasts.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public Task Keep_drops_blobs_that_left_the_head()
    {
        var run = Create();
        foreach (var number in new[] { 1, 2 })
        {
            run.Begin(_repo, Blob(number));
            run = Restart(run);
            _ = run.Recover(_repo);
        }

        Require(run.Keep(_repo, new HashSet<string>(StringComparer.Ordinal) { Blob(2), Blob(3) }) == 1, "one of the two is still in the head");
        var later = Restart(run);
        Require(!later.Contains(_repo, Blob(1)) && later.Contains(_repo, Blob(2)), "the drop lasts across a restart");
        return Task.CompletedTask;
    }

    /// <summary>The list holds at most <see cref="CodeIndexRules.MaxQuarantined"/> blobs and the oldest go first.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public Task The_list_is_capped_and_the_oldest_go_first()
    {
        var run = Create();
        for (var number = 1; number <= CodeIndexRules.MaxQuarantined + 5; number++)
        {
            run.Begin(_repo, Blob(number));
            run = Restart(run);
            _ = run.Recover(_repo);
        }

        Require(run.Recover(_repo) == CodeIndexRules.MaxQuarantined, "the list is at the cap");
        Require(!run.Contains(_repo, Blob(1)) && run.Contains(_repo, Blob(CodeIndexRules.MaxQuarantined + 5)), "the oldest went, the newest stayed");
        return Task.CompletedTask;
    }

    /// <summary>Repos do not share marks or lists.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public Task Repos_are_kept_apart()
    {
        var died = Create();
        died.Begin(_repo, Blob(1));
        var next = Restart(died);

        Require(next.Recover(_other) == 0 && !next.Contains(_other, Blob(1)), "the other repo recovers nothing");
        Require(next.Recover(_repo) == 1, "the marked repo recovers its blob");
        return Task.CompletedTask;
    }

    /// <summary>Only a git object id can be marked.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public Task A_mark_takes_only_a_blob_id()
    {
        var quarantine = Create();

        foreach (var bad in new[] { "", "short", "../../etc/passwd", new string('g', 40), new string('a', 65) })
        {
            try
            {
                quarantine.Begin(_repo, bad);
            }
            catch (ArgumentException)
            {
                continue;
            }

            throw new ContractViolationException($"contract: Begin accepted '{bad}'");
        }

        return Task.CompletedTask;
    }

    private static string Blob(int number) => number.ToString("x", System.Globalization.CultureInfo.InvariantCulture).PadLeft(40, '0');

    private static void Require(bool condition, string what)
    {
        if (!condition)
        {
            throw new ContractViolationException($"contract: {what}");
        }
    }
}
