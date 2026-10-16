using System.Globalization;
using System.Text.Json;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.BugFix.Infrastructure;

/// <summary>state.json in the state directory, written to a temporary file and renamed so a crash never leaves half a state.</summary>
internal sealed class FileStateStore(JsonSerializerOptions json, BugFixSettings settings) : IStateStore
{
    private static readonly TimeSpan _lockPause = TimeSpan.FromMilliseconds(50);

    public string StateFile(string directory) => Path.Combine(directory, "state.json");

    // An exclusive lock file: the OS releases it if the process dies, so a crash never leaves the directory locked.
    public Result<IDisposable> Lock(string directory, bool create)
    {
        var lockFile = Path.Combine(directory, "state.lock");
        if (settings.FileLockingDisabled)
        {
            return BugFixErrors.StorageFailed.With("DOTNET_SYSTEM_IO_DISABLEFILELOCKING turns file locks off, so the state could be written by two calls at once; unset it");
        }

        if (!create && !Directory.Exists(directory))
        {
            return BugFixErrors.StateMissing.With($"no state at {StateFile(directory)}; run bugfix-state init first");
        }

        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            try
            {
                _ = Directory.CreateDirectory(directory);
                return new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            // A held lock is an IOException on an existing lock file; once state.lock exists (it stays after the first init) any
            // other open failure looks the same and is reported as busy after the wait.
            catch (IOException) when (File.Exists(lockFile) && waited.Elapsed < settings.LockWait)
            {
#pragma warning disable RS0030 // bugfix-state is a synchronous CLI; this waits out another call's lock, bounded by LockWait
                Thread.Sleep(_lockPause);   // another call holds it: wait
#pragma warning restore RS0030
            }
            catch (IOException ex) when (File.Exists(lockFile))
            {
                return BugFixErrors.StateBusy.With($"another bugfix-state call holds {lockFile} ({ex.Message}); retry when it finishes");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return BugFixErrors.StorageFailed.With($"cannot lock {lockFile}: {ex.Message}");
            }
        }
    }

    public Result<BugFixState> Load(string directory)
    {
        var file = StateFile(directory);
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(file);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return BugFixErrors.StateMissing.With($"no state at {file}; run bugfix-state init first");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return BugFixErrors.StateCorrupt.With($"cannot read {file}: {ex.Message}");
        }

        var state = AwJson.Deserialize<BugFixState>(bytes, json);
        if (state.IsError || state.Value.SchemaVersion != BugFixState.CurrentSchema)
        {
            return BugFixErrors.StateCorrupt.With(
                $"invalid state file {file}: {(state.IsError ? state.Error.Message : string.Create(CultureInfo.InvariantCulture, $"schema version {state.Value.SchemaVersion}"))}");
        }

        return state;
    }

    public void Save(string directory, BugFixState state)
    {
        _ = Directory.CreateDirectory(directory);
        var file = StateFile(directory);
        var temporary = string.Create(CultureInfo.InvariantCulture, $"{file}.{Environment.ProcessId}.tmp");
        File.WriteAllBytes(temporary, AwJson.Serialize(state, json));
        File.Move(temporary, file, overwrite: true);
    }
}
