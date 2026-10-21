using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.CodeIndex.Infrastructure.Snapshot;

/// <summary>
/// Blob contents by object id from one long-lived 'git cat-file --batch' (spec §5.1). A request is the id and a newline; the
/// answer is "&lt;id&gt; &lt;type&gt; &lt;size&gt;", the bytes and a newline, or "&lt;id&gt; missing". In a promisor clone git does not answer
/// for an object it lacks: it tries a fetch, which the protocol guard refuses, and exits. That read answers null and the next
/// read starts a new process, so a missing blob costs a process, never a network call.
/// </summary>
internal sealed partial class GitBlobReader(string repo) : IBlobReader
{
    private readonly SemaphoreSlim _one = new(1, 1);
    private Session? _session;

    /// <inheritdoc />
    public async ValueTask<Result<byte[]?>> Read(string blob, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(blob);
        if (!ObjectId.IsMatch(blob))
        {
            return (byte[]?)null;
        }

        await _one.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _session ??= Session.Start(repo);
            try
            {
                var bytes = await _session.Read(blob, cancellationToken).ConfigureAwait(false);
                if (bytes.Ended)
                {
                    EndSession();
                }

                return bytes.Content;
            }
            catch (OperationCanceledException)
            {
                EndSession();   // the answer is half read: the pipe is out of step, so the next read starts afresh
                throw;
            }
        }
        finally
        {
            _ = _one.Release();
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        EndSession();
        _one.Dispose();
        return ValueTask.CompletedTask;
    }

    [GeneratedRegex("^[0-9a-f]{40}([0-9a-f]{24})?\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ObjectId { get; }

    private void EndSession()
    {
        _session?.Dispose();
        _session = null;
    }

    private sealed class Session : IDisposable
    {
        private const int BufferSize = 64 * 1024;
        private readonly Process _git;
        private readonly Stream _out;
        private readonly byte[] _buffer = new byte[BufferSize];
        private int _start;
        private int _end;

        private Session(Process git)
        {
            _git = git;
            _out = git.StandardOutput.BaseStream;
        }

        public static Session Start(string repo)
        {
            var git = Process.Start(CleanGit.Prepare(repo, redirectInput: true, "cat-file", "--batch"))
                ?? throw new InvalidOperationException("git didn't start");
            _ = git.StandardError.BaseStream.CopyToAsync(Stream.Null);   // drained, never kept: it may name repo content
            return new Session(git);
        }

        /// <summary>Asks for one object. Ended is true when git went away (the object is missing or the process died), Content is null for any object that did not come back as a blob.</summary>
        public async Task<(byte[]? Content, bool Ended)> Read(string blob, CancellationToken cancellationToken)
        {
            try
            {
                await _git.StandardInput.BaseStream.WriteAsync(Encoding.ASCII.GetBytes(blob + "\n"), cancellationToken).ConfigureAwait(false);
                await _git.StandardInput.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return (null, true);
            }

            var header = await ReadLine(cancellationToken).ConfigureAwait(false);
            if (header is null)
            {
                return (null, true);
            }

            if (header.Split(' ') is not [_, var type, var sizeText] || !long.TryParse(sizeText, NumberStyles.None, CultureInfo.InvariantCulture, out var size))
            {
                return (null, false);   // "<id> missing"
            }

            if (size > Array.MaxLength)
            {
                return (null, true);   // never held in memory: the process is dropped with the rest of the answer unread
            }

            var content = new byte[size];
            if (!await ReadExactly(content, cancellationToken).ConfigureAwait(false) || !await ReadExactly(new byte[1], cancellationToken).ConfigureAwait(false))
            {
                return (null, true);
            }

            return (type == "blob" ? content : null, false);
        }

        public void Dispose()
        {
            try
            {
                _git.StandardInput.Close();
            }
            catch (IOException)
            {
                // git was already gone
            }

            if (!_git.WaitForExit(TimeSpan.FromSeconds(2)))
            {
                CleanGit.Kill(_git);
            }

            _git.Dispose();
        }

        private async Task<string?> ReadLine(CancellationToken cancellationToken)
        {
            var line = new StringBuilder();
            while (true)
            {
                if (_start == _end && !await Fill(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                var b = _buffer[_start++];
                if (b == (byte)'\n')
                {
                    return line.ToString();
                }

                if (line.Length > 200)
                {
                    return null;   // not a header git writes
                }

                _ = line.Append((char)b);
            }
        }

        private async Task<bool> ReadExactly(byte[] target, CancellationToken cancellationToken)
        {
            var filled = 0;
            while (filled < target.Length)
            {
                if (_start == _end && !await Fill(cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                var take = Math.Min(_end - _start, target.Length - filled);
                Buffer.BlockCopy(_buffer, _start, target, filled, take);
                _start += take;
                filled += take;
            }

            return true;
        }

        private async Task<bool> Fill(CancellationToken cancellationToken)
        {
            _start = 0;
            _end = await _out.ReadAsync(_buffer, cancellationToken).ConfigureAwait(false);
            return _end > 0;
        }
    }
}
