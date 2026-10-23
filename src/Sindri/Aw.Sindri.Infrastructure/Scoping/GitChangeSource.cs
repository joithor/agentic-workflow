using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Infrastructure.Scoping;

/// <summary>
/// The change port over git (scoping spec §4, C7). A branch resolves to its head and the merge base with the default branch; a PR number asks <c language="text">gh pr view</c> for its base and head shas and body
/// (the head must already be in the checkout: fetch <c language="text">pull/N/head</c> first). The diff is the one against the merge base of the paths that may be read, so a hunk under a denied path (a secret, a key) is never read, and the paths are listed as denied.
/// </summary>
internal sealed class GitChangeSource : IChangeSource
{
    private static readonly string[] _defaultBranches = ["origin/HEAD", "origin/main", "origin/master", "main", "master"];

    public async ValueTask<Result<ChangeRecord>> Read(string checkout, string reference, ChangeRead read, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(read);
        var resolved = await Resolve(checkout, reference, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var (@base, head, body) = resolved.Value;
        var names = await Git(checkout, ["diff", "--name-only", "--no-renames", "-z", @base, head], cancellationToken).ConfigureAwait(false);
        var messages = await Git(checkout, ["log", "--reverse", "--format=%B%x00", $"{@base}..{head}"], cancellationToken).ConfigureAwait(false);
        if (names.IsError || messages.IsError)
        {
            return names.IsError ? names.Error : messages.Error;
        }

        var paths = names.Value.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var readable = paths.Where(static p => !BriefPaths.Denied(p)).ToList();
        var diff = await Diff(checkout, @base, head, readable, cancellationToken).ConfigureAwait(false);
        if (diff.IsError)
        {
            return diff.Error;
        }

        var (text, truncated) = read.Cut(diff.Value);
        return new ChangeRecord(reference, @base, head, body, [.. messages.Value.Split('\0', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)], readable, [.. paths.Where(BriefPaths.Denied)], text, truncated);
    }

    private static async ValueTask<Result<string>> Git(string checkout, string[] args, CancellationToken cancellationToken)
    {
        var run = await GitProcess.Run(checkout, args, cancellationToken).ConfigureAwait(false);
        return run.Exit == 0 ? run.Output : SindriErrors.InvalidInput.With($"git {args[0]} failed in {checkout}: {run.Error.Trim()}");
    }

    private static async ValueTask<Result<string>> Diff(string checkout, string @base, string head, List<string> readable, CancellationToken cancellationToken) =>
        readable.Count == 0
            ? string.Empty
            : await Git(checkout, ["diff", "--no-color", "--no-renames", @base, head, "--", .. readable.Select(static p => ":(literal)" + p)], cancellationToken).ConfigureAwait(false);

    private static async ValueTask<Result<(string Base, string Head, string? Body)>> Resolve(string checkout, string reference, CancellationToken cancellationToken)
    {
        if (reference.All(char.IsAsciiDigit) && reference.Length > 0)
        {
            return await PullRequest(checkout, reference, cancellationToken).ConfigureAwait(false);
        }

        var head = await Commit(checkout, reference, cancellationToken).ConfigureAwait(false) ?? await Commit(checkout, "origin/" + reference, cancellationToken).ConfigureAwait(false);
        if (head is null)
        {
            return SindriErrors.InvalidInput.With($"the checkout {checkout} has no branch '{reference}' (fix: fetch it, or name a PR number)");
        }

        foreach (var candidate in _defaultBranches)
        {
            var @base = await Git(checkout, ["merge-base", candidate, head], cancellationToken).ConfigureAwait(false);
            if (@base.IsSuccess)
            {
                return (@base.Value, head, null);
            }
        }

        return SindriErrors.InvalidInput.With($"the checkout {checkout} has no default branch (origin/HEAD, main or master) to take '{reference}' against");
    }

    private static async ValueTask<string?> Commit(string checkout, string name, CancellationToken cancellationToken)
    {
        var run = await GitProcess.Run(checkout, ["rev-parse", "--verify", "--quiet", name + "^{commit}"], cancellationToken).ConfigureAwait(false);
        return run.Exit == 0 && run.Output.Length > 0 ? run.Output : null;
    }

    private static async ValueTask<Result<(string Base, string Head, string? Body)>> PullRequest(string checkout, string number, CancellationToken cancellationToken)
    {
        var pr = await Gh(checkout, ["pr", "view", number, "--json", "headRefOid,baseRefOid,body"], cancellationToken).ConfigureAwait(false);
        if (pr.IsError)
        {
            return pr.Error;
        }

        try
        {
            using var json = JsonDocument.Parse(pr.Value);
            var head = json.RootElement.GetProperty("headRefOid").GetString() ?? string.Empty;
            var baseSha = json.RootElement.GetProperty("baseRefOid").GetString() ?? string.Empty;
            var body = json.RootElement.TryGetProperty("body", out var text) ? text.GetString() : null;
            if (await Commit(checkout, head, cancellationToken).ConfigureAwait(false) is null)
            {
                return SindriErrors.InvalidInput.With($"PR {number}'s head {head} is not in the checkout (fix: git fetch origin pull/{number}/head)");
            }

            var mergeBase = await Git(checkout, ["merge-base", baseSha, head], cancellationToken).ConfigureAwait(false);
            return mergeBase.IsError ? mergeBase.Error : (mergeBase.Value, head, body);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return SindriErrors.InvalidInput.With($"gh pr view {number} answered something unexpected: {e.Message}");   // boundary: a tool's output
        }
    }

    private static async ValueTask<Result<string>> Gh(string checkout, string[] args, CancellationToken cancellationToken)
    {
        var start = ChildEnvironment.Start("gh");
        start.WorkingDirectory = checkout;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        try
        {
            using var gh = Process.Start(start);
            if (gh is null)
            {
                return SindriErrors.InvalidInput.With("gh did not start");
            }

            var output = gh.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = gh.StandardError.ReadToEndAsync(cancellationToken);
            await gh.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return gh.ExitCode == 0 ? await output.ConfigureAwait(false) : SindriErrors.InvalidInput.With($"gh {args[0]} {args[1]} failed: {(await error.ConfigureAwait(false)).Trim()}");
        }
        catch (Win32Exception e) // boundary: no gh on PATH
        {
            return SindriErrors.InvalidInput.With($"a PR number needs the gh CLI: {e.Message} (fix: install gh, or name the branch)");
        }
    }
}
