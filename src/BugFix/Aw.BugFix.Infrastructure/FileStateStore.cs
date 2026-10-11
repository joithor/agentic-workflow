using System.Globalization;
using System.Text.Json;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.BugFix.Infrastructure;

/// <summary>state.json in the state directory, written to a temporary file and renamed so a crash never leaves half a state.</summary>
internal sealed class FileStateStore(JsonSerializerOptions json) : IStateStore
{
    private const int LockAttempts = 300;
    private static readonly TimeSpan _lockPause = TimeSpan.FromMilliseconds(100);

    public string StateFile(string directory) => Path.Combine(directory, "state.json");

    // An exclusive lock file: the OS releases it if the process dies, so a crash never leaves the directory locked.
    public Result<IDisposable> Lock(string directory)
    {
        var lockFile = Path.Combine(directory, "state.lock");
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                _ = Directory.CreateDirectory(directory);
                return new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (attempt < LockAttempts)
            {
                Thread.Sleep(_lockPause);
            }
            catch (IOException ex)
            {
                return BugFixErrors.StateBusy.With($"another bugfix-state call holds {lockFile} ({ex.Message}); retry when it finishes");
            }
            catch (UnauthorizedAccessException ex)
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
