using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Packets.Core;
using Aw.Packets.Core.Model;

namespace Aw.Packets.App.Infrastructure;

/// <summary>
/// Writes freeze records under the packets/ state namespace, and nowhere else: a new record is written to a
/// temporary file beside it and moved into place, so a reader never sees half a record and a second freeze of the
/// same hash changes nothing.
/// </summary>
/// <param name="settings">The state directory (…/packets).</param>
/// <param name="json">The record options (AwJson, indented).</param>
internal sealed class FileFreezeLog(PacketsSettings settings, JsonSerializerOptions json) : IFreezeLog
{
    public ValueTask<Result<FreezeReceipt>> Record(FreezeRecord record, CancellationToken cancellationToken)
    {
        var location = FreezeLocation.Of(record);
        return ValueTask.FromResult(location.IsError ? location.Error : Write(record, location.Value));
    }

    private Result<FreezeReceipt> Write(FreezeRecord record, string location)
    {
        var target = Path.Combine(settings.StateDirectory, location);
        var temporary = $"{target}.{Guid.NewGuid():N}.tmp";
        try
        {
            if (File.Exists(target))
            {
                return new FreezeReceipt(location, AlreadyFrozen: true);
            }

            _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!); // why: target is a file path under the state directory, so it has a parent.
            File.WriteAllBytes(temporary, AwJson.Serialize(record, json));
            File.Move(temporary, target, overwrite: false);
            return new FreezeReceipt(location, AlreadyFrozen: false);
        }
        catch (IOException) when (File.Exists(target))
        {
            return new FreezeReceipt(location, AlreadyFrozen: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return PacketsErrors.FreezeLogUnavailable.With($"Can't write {location} under {settings.StateDirectory}: {ex.Message}");
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
