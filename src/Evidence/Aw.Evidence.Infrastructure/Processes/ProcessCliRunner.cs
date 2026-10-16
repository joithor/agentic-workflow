using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Processes;

/// <summary>
/// Runs a program in a fresh temporary directory (its working directory, HOME and TMPDIR) with a cleared environment: a fixed
/// PATH plus the given variables. The program is an absolute path, never looked up on PATH.
/// </summary>
internal sealed class ProcessCliRunner(TimeSpan timeout, string? tempRoot = null) : ICliRunner
{
    private const string FixedPath = "/usr/local/bin:/usr/bin:/bin";

    /// <inheritdoc/>
    public async ValueTask<Result<CliOutcome>> RunAsync(CliInvocation invocation, CancellationToken cancellationToken)
    {
        if (!invocation.Executable.StartsWith('/'))
        {
            return EvidenceErrors.RunFailed.With($"'{invocation.Executable}' is not an absolute path: a program is never looked up on PATH");
        }

        var made = MakeDirectory(tempRoot);
        if (made.IsError)
        {
            return made.Error;
        }

        var directory = made.Value;
        try
        {
            var environment = new Dictionary<string, string>(invocation.Environment, StringComparer.Ordinal)
            {
                ["PATH"] = FixedPath,
                ["HOME"] = directory.FullName,
                ["TMPDIR"] = directory.FullName,
            };
            var spec = new ProcessSpec([invocation.Executable, .. invocation.Args], invocation.Stdin ?? string.Empty, environment, directory.FullName);
            var outcome = await ProcessRunner.RunAsync(spec, timeout, EvidenceErrors.RunFailed, cancellationToken).ConfigureAwait(false);
            return outcome.Map(static o => new CliOutcome(o.ExitCode, o.StandardOutput, o.StandardError));
        }
        finally
        {
            Remove(directory);
        }
    }

    private static Result<DirectoryInfo> MakeDirectory(string? root)
    {
        try
        {
            return root is null ? Directory.CreateTempSubdirectory("aw-api-") : Directory.CreateDirectory(Path.Combine(root, "aw-api-" + Path.GetRandomFileName()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return EvidenceErrors.RunFailed.With($"the program's temporary directory could not be made: {ex.Message}");
        }
    }

    // The call's scratch space is its own: nothing of it is kept, and a leftover must not fail a run.
    private static void Remove(DirectoryInfo directory)
    {
        try
        {
            directory.Delete(recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Something still holds a file, or the program left a directory it made read-only; the system temp cleaner owns it now.
        }
    }
}
