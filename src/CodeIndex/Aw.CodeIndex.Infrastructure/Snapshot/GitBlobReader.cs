using System.Diagnostics;
using System.Globalization;
using System.Text;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.CodeIndex.Infrastructure.Snapshot;

/// <summary>One long-lived 'git cat-file --batch' (spec §5.1). Reads are serialized: the process answers in order on one pipe.</summary>
internal sealed class GitBlobReader : IBlobReader
{
    private static readonly TimeSpan _exitGrace = TimeSpan.FromSeconds(2);
    private readonly Process _git;
    private readonly Stream _input;
    private readonly BufferedStream _output;
    private readonly Task _stderr;
    private readonly SemaphoreSlim _turn = new(1, 1);
    private bool _broken;

    private GitBlobReader(Process git)
    {
        _git = git;
        _input = git.StandardInput.BaseStream;
        _output = new BufferedStream(git.StandardOutput.BaseStream, 64 * 1024);
        _stderr = git.StandardError.BaseStream.CopyToAsync(Stream.Null);
    }

    /// <summary>Starts the process.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <returns>The reader, or SND-CODEIDX-002 when git does not start.</returns>
    internal static Result<IBlobReader> Start(string repo)
    {
        var git = Process.Start(CleanGit.Prepare(repo, redirectInput: true, "cat-file", "--batch"));
        return git is null ? CodeIndexErrors.RepoNotFound.With("git did not start") : new GitBlobReader(git);
    }

    /// <inheritdoc />
    public async ValueTask<Result<byte[]?>> Read(string blob, CancellationToken cancellationToken)
    {
        if (!IsObjectId(blob))
        {
            return (byte[]?)null;
        }

        await _turn.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_broken)
            {
                return CodeIndexErrors.RepoNotFound.With("the git reader stopped");
            }

            return await Exchange(blob, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Break();   // the pipe is mid-answer; the next read would be out of step
            throw;
        }
        finally
        {
            _ = _turn.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _broken = true;
        try
        {
            _input.Close();
            using var grace = new CancellationTokenSource(_exitGrace);
            await _git.WaitForExitAsync(grace.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is OperationCanceledException or IOException)
        {
            Break();
        }

        _ = await Task.WhenAny(_stderr, Task.Delay(_exitGrace)).ConfigureAwait(false);
        await _output.DisposeAsync().ConfigureAwait(false);
        _git.Dispose();
        _turn.Dispose();
    }

    private static bool IsObjectId(string blob) => blob.Length is 40 or 64 && blob.All(char.IsAsciiHexDigit);

    private async ValueTask<Result<byte[]?>> Exchange(string blob, CancellationToken cancellationToken)
    {
        try
        {
            await _input.WriteAsync(Encoding.ASCII.GetBytes(blob + "\n"), cancellationToken).ConfigureAwait(false);
            await _input.FlushAsync(cancellationToken).ConfigureAwait(false);
            var header = await ReadLine(cancellationToken).ConfigureAwait(false);
            if (header is null)
            {
                return Stopped();
            }

            if (header.EndsWith(" missing", StringComparison.Ordinal))
            {
                return (byte[]?)null;
            }

            var parts = header.Split(' ');
            if (parts.Length != 3 || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var size))
            {
                return Stopped();
            }

            var bytes = new byte[size];
            await _output.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            _ = await ReadLine(cancellationToken).ConfigureAwait(false);   // the newline git ends each object with
            return bytes;
        }
        catch (Exception e) when (e is IOException or EndOfStreamException)
        {
            return Stopped();
        }
    }

    private async ValueTask<string?> ReadLine(CancellationToken cancellationToken)
    {
        var line = new List<byte>(64);
        var one = new byte[1];
        while (true)
        {
            if (await _output.ReadAsync(one, cancellationToken).ConfigureAwait(false) == 0)
            {
                return null;
            }

            if (one[0] == (byte)'\n')
            {
                return Encoding.ASCII.GetString([.. line]);
            }

            line.Add(one[0]);
        }
    }

    private Result<byte[]?> Stopped()
    {
        Break();
        return CodeIndexErrors.RepoNotFound.With("git stopped answering");
    }

    private void Break()
    {
        _broken = true;
        try
        {
            if (!_git.HasExited)
            {
                _git.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // it exited between the check and the kill
        }
    }
}
