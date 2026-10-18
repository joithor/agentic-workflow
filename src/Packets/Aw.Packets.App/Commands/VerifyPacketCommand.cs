using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Packets.App.Features.VerifyPacket;
using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Values;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.App.Commands;

/// <summary>
/// The 'verify' command: does the packet still hash to '--hash', and was that hash frozen (and by whom)? Exit 0
/// when so, 1 on a mismatch or when it was never frozen, 2 for a usage error or a packet that can't be read.
/// </summary>
internal static class VerifyPacketCommand
{
    internal const string Name = "verify";
    private const string Shape = "<packet-dir> --hash <sha256:hex> [--candidate] [--kind build|scoping|design|plan|phase|proof|review|handoff] [--repo-slug <owner/name> --instance <slug> (scoping)] [--json]";

    internal static async Task<int> Run(PacketKind kind, IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (Parse(kind, args) is not { } request)
        {
            return CommandOutput.Usage($"{Name} {Shape}");
        }

        var service = services.GetRequiredService<IService<VerifyPacketRequest, VerifyPacketResponse>>();
        var result = await service.Handle(request.Request, CancellationToken.None).ConfigureAwait(false);
        if (result.IsError)
        {
            return CommandOutput.Error(result.Error, request.Json, json);
        }

        var verdict = result.Value;
        if (request.Json)
        {
            return CommandOutput.Json(verdict, verdict.Verified, json);
        }

        Console.Out.WriteLine($"aw-packets verify: {Subject(verdict)} — {Outcome(verdict)}");
        return verdict.Verified ? 0 : 1;
    }

    private static string Subject(VerifyPacketResponse verdict) =>
        $"{(verdict.Kind == PacketKinds.Build.Name ? string.Empty : verdict.Kind + " ")}packet {verdict.Packet?.Value ?? "(unknown)"}";

    private static string Outcome(VerifyPacketResponse verdict) => verdict switch
    {
        { Verified: true, Candidate: true } => $"hash matches {verdict.Expected.Value} (candidate: no freeze record looked up)",
        { Verified: true } => $"verified {verdict.Expected.Value}, frozen by {By(verdict)} at {verdict.FrozenAt?.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)} (packets/{verdict.Location})",
        { Frozen: null } => $"MISMATCH: {verdict.Reason}",
        _ => $"NOT FROZEN: {verdict.Reason}",
    };

    private static string By(VerifyPacketResponse verdict) => verdict.By is Core.Model.FreezeBy.Owner ? "owner" : "machine";

    private static (VerifyPacketRequest Request, bool Json)? Parse(PacketKind kind, IReadOnlyList<string> args)
    {
        if (SimpleArguments.Parse(args, ["--candidate"], "--hash", "--repo-slug", "--instance") is not { } parsed
            || !parsed.Values.TryGetValue("--hash", out var text)
            || !PacketHash.TryFrom(text, out var hash))
        {
            return null;
        }

        var candidate = parsed.Switches.Contains("--candidate");
        var slug = parsed.Values.GetValueOrDefault("--repo-slug");
        var instance = parsed.Values.GetValueOrDefault("--instance");
        var request = new VerifyPacketRequest(kind.Name, parsed.PacketDirectory, hash, candidate, slug, instance);
        return NamesRight(kind, request) ? (request, parsed.Json) : null;
    }

    // Only a scope packet takes --repo-slug and --instance (it has no packet.json to say them), and unless it is a candidate it needs both.
    private static bool NamesRight(PacketKind kind, VerifyPacketRequest request)
    {
        if (kind != PacketKinds.Scoping)
        {
            return request.RepoSlug is null && request.Instance is null;
        }

        return request.Candidate || (request.RepoSlug is { } slug && RepoSlug.TryFrom(slug, out _) && request.Instance is { } instance && PacketId.TryFrom(instance, out _));
    }
}
