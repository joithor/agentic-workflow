using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Packets.Core;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.App.Infrastructure;

/// <summary>
/// Writes freeze records under the packets/ state namespace, and nowhere else: a new record is written to a
/// temporary file beside it and moved into place, so a reader never sees half a record and a second freeze of the
/// same hash changes nothing.
/// </summary>
/// <param name="settings">The state directory (…/packets).</param>
/// <param name="json">The package's JSON options (the one AwJson profile Composition builds).</param>
internal sealed class FileFreezeLog(PacketsSettings settings, JsonSerializerOptions json) : IFreezeLog
{
    public ValueTask<Result<FreezeReceipt>> Record(FreezeRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        return ValueTask.FromResult(Write(record, FreezeLocation.Of(record)));
    }

    public async ValueTask<Result<FreezeRecord?>> Find(PacketIdentity packet, PacketHash hash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var location = FreezeLocation.Of(packet.Repo, packet.Packet, hash);
        var target = Path.Combine(settings.StateDirectory, location);
        try
        {
            if (!File.Exists(target))
            {
                return Results.Success<FreezeRecord?>(value: null);
            }

            var bytes = await File.ReadAllBytesAsync(target, cancellationToken).ConfigureAwait(false);
            var parsed = AwJson.Deserialize<FreezeRecord>(bytes, json);
            return parsed.IsError
                ? PacketsErrors.Unreadable.With($"The freeze record {location} can't be read: {parsed.Error.Message}")
                : Results.Success<FreezeRecord?>(parsed.Value);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return PacketsErrors.Unreadable.With($"The freeze record {location} can't be read: {ex.Message}");
        }
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
            RemoveIfLeft(temporary);
        }
    }

    private static void RemoveIfLeft(string temporary)
    {
        try
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A temporary file that can't be removed is left beside the record; the record itself is already decided.
        }
    }
}
