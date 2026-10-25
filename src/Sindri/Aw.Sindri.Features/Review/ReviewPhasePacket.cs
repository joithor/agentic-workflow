using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.Review;

/// <summary>The phase packet's files the review result names: its packet.json (id, repo, base commit) and its acceptance.json (whose hash the result's index carries).</summary>
/// <param name="Id">packet.json's id.</param>
/// <param name="Repo">packet.json's repo ('owner/name').</param>
/// <param name="BaseSha">packet.json's baseSha.</param>
/// <param name="AcceptanceText">acceptance.json's text.</param>
internal sealed record ReviewPhasePacket(string Id, string Repo, string BaseSha, string AcceptanceText)
{
    /// <summary>Reads the files from the phase packet's directory.</summary>
    /// <param name="files">The reader.</param>
    /// <param name="dir">The packet directory.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The files, or the reason one could not be read.</returns>
    internal static async ValueTask<(ReviewPhasePacket? Packet, string? Failure)> Load(IBriefFiles files, string dir, CancellationToken cancellationToken)
    {
        var packet = await files.Read("packet.json", [dir], cancellationToken).ConfigureAwait(false);
        if (packet.IsError)
        {
            return (null, $"packet.json: {packet.Error.Message}");
        }

        var acceptance = await files.Read("acceptance.json", [dir], cancellationToken).ConfigureAwait(false);
        return acceptance.IsError ? (null, $"acceptance.json: {acceptance.Error.Message}") : Parse(packet.Value.Text, acceptance.Value.Text);
    }

    private static (ReviewPhasePacket? Packet, string? Failure) Parse(string packetText, string acceptanceText)
    {
        try
        {
            var packet = JsonNode.Parse(packetText);
            return packet?["id"] is null || packet["repo"] is null || packet["baseSha"] is null
                ? (null, "packet.json must name the packet's id, repo and baseSha")
                : (new ReviewPhasePacket((string)packet["id"]!, (string)packet["repo"]!, (string)packet["baseSha"]!, acceptanceText), null); // why: the null checks above
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return (null, $"packet.json is not the JSON it should be: {e.Message}");
        }
    }
}
