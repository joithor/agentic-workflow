using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The spawned child of a viewer and its stderr (the part of <see cref="ViewerProcess"/> that owns the process).</summary>
internal static partial class ViewerProcess
{
    /// <summary>A started child: stdin closed, stdout discarded, the first 2 KB of stderr kept (the rest is drained).</summary>
    internal sealed class Child(Process process)
    {
        private readonly StringBuilder _stderr = new();

        /// <summary>The file a detached window's stderr goes to (its own, so no pipe to Sindri stays open for the window's life), or null when stderr is piped.</summary>
        public string? ErrorFile { get; init; }

        public Process Process { get; } = process;

        public Task Exited { get; } = process.WaitForExitAsync(CancellationToken.None);

        public Task Drained { get; private set; } = Task.CompletedTask;

        public void Drain() => Drained = ErrorFile is not null ? Task.CompletedTask : Task.Run(async () =>
        {
            var buffer = new char[512];
            int read;
            while ((read = await Process.StandardError.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                lock (_stderr)
                {
                    _ = _stderr.Append(buffer, 0, Math.Min(read, StderrLimit - _stderr.Length));
                }
            }
        });

        public string Stderr()
        {
            if (ErrorFile is { } file)
            {
                return ReadHead(file);
            }

            lock (_stderr)
            {
                return _stderr.ToString();
            }
        }
    }

    private static string ReadHead(string file)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[StderrLimit];
            var read = stream.Read(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer, 0, read);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static void DeleteQuietly(string? file)
    {
        if (file is null)
        {
            return;
        }

        try
        {
            File.Delete(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a temp file that stays is the OS's to clean
        }
    }

    /// <summary>Ends a child that is still running, with its children (an opener that ran past its budget); it is not disposed.</summary>
    /// <param name="child">The child.</param>
    internal static void KillTree(Child child)
    {
        try
        {
            if (!child.Process.HasExited)
            {
                child.Process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception)
        {
            // it ended between the check and the kill
        }
    }
}
