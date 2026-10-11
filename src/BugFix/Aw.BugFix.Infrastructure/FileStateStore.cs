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
    public string StateFile(string directory) => Path.Combine(directory, "state.json");

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
