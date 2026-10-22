using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Line;

/// <summary>What the design and plan stations do with packets: verify a hash, write the files of a packet they assembled into the run area, turn aw-packets' failures into findings.</summary>
internal static class LinePackets
{
    /// <summary>The packet directory of the design station's candidate in its run area.</summary>
    internal const string DesignDirectory = "design-packet";

    /// <summary>The packet directory of the plan station's candidate in its run area.</summary>
    internal const string PlanDirectory = "plan-packet";

    /// <summary>The directory (below the run area) holding one packet directory per phase.</summary>
    internal const string PhaseDirectory = "phase-packets";

    /// <summary>The owner and the repo's name aw-packets files a packet under ('owner/name'): the profile names no remote, so the owner is 'sindri'.</summary>
    /// <param name="context">The Step's context.</param>
    /// <returns>The slug, or null when the instance has no repo.</returns>
    internal static string? RepoSlug(StepContext context) => PacketSlugs.RepoSlug(context.Repo);

    /// <summary>Recomputes a packet's hash from its directory and compares it with the fact's (a candidate: no freeze record is looked up; the port has no verify).</summary>
    /// <param name="packets">aw-packets.</param>
    /// <param name="kind">The packet's kind.</param>
    /// <param name="packet">The fact.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The packet; SND-LINE-001 naming both hashes when they differ; the tool's error when it could not run.</returns>
    internal static async ValueTask<Result<PacketFact>> Verify(IPackets packets, PacketKind kind, PacketFact packet, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packets);
        ArgumentNullException.ThrowIfNull(packet);
        var computed = await packets.Hash(new PacketRef(kind, packet.Directory), cancellationToken).ConfigureAwait(false);
        if (computed.IsError)
        {
            return computed.Error;
        }

        return string.Equals(computed.Value, packet.Hash, StringComparison.Ordinal)
            ? packet
            : SindriErrors.InvalidInput.With($"SND-LINE-001: the {kind.ToString().ToLowerInvariant()} packet in {packet.Directory} hashes to {computed.Value}, not the {packet.Hash} the fact names (fix: re-run the station that made it; a packet is never edited)");
    }

    /// <summary>Writes a packet's files into the instance's run area, replacing what a previous round wrote.</summary>
    /// <param name="area">The run area.</param>
    /// <param name="address">The instance's address.</param>
    /// <param name="directory">The packet directory below the run area.</param>
    /// <param name="files">The files.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The packet directory's absolute path; the first write's error.</returns>
    internal static async ValueTask<Result<string>> Write(IRunArea area, string address, string directory, IEnumerable<PacketText> files, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(files);
        foreach (var file in files)
        {
            var written = await area.Write(address, directory + "/" + file.Name, file.Content, append: false, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written.Error;
            }
        }

        return area.PathFor(address, directory);
    }

    /// <summary>aw-packets' failed checks as findings, so the station's findings fact names the rule and the fix.</summary>
    /// <param name="failures">The failures.</param>
    /// <returns>The findings.</returns>
    internal static IEnumerable<LineFinding> Findings(IEnumerable<PacketFailure> failures) =>
        failures.Select(static f => new LineFinding(f.Rule, f.Subject, f.Message, f.Fix ?? "Fix the packet file the rule names."));

    /// <summary>The findings as the 'fact:findings' array (rule, item, message, fix, severity).</summary>
    /// <param name="findings">The failing findings.</param>
    /// <returns>The array.</returns>
    internal static JsonArray FindingsFact(IEnumerable<LineFinding> findings)
    {
        var array = new JsonArray();
        foreach (var f in findings)
        {
            LineJson.Append(array, new JsonObject { ["rule"] = f.Rule, ["item"] = f.Item, ["message"] = f.Message, ["fix"] = f.Fix, ["severity"] = "fail" });
        }

        return array;
    }

    /// <summary>A completed result that writes the findings: the outcome the Step declares for findings left ('incomplete' at an evidence stage, 'mismatch' at a freeze).</summary>
    /// <param name="outcome">The outcome.</param>
    /// <param name="findings">The findings.</param>
    /// <returns>The result.</returns>
    internal static BlockStepResult Gaps(string outcome, IEnumerable<LineFinding> findings) => StepFacts.Done(outcome, ("fact:findings", FindingsFact(findings)));

    /// <summary>A failing finding about a packet the Step could not assemble or verify.</summary>
    /// <param name="rule">The rule id.</param>
    /// <param name="message">What is wrong.</param>
    /// <param name="fix">What to change.</param>
    /// <returns>The finding.</returns>
    internal static LineFinding Finding(string rule, string message, string fix) => new(rule, Item: null, message, fix);

    /// <summary>'sha256:' and the lower-case hex of the text's UTF-8 bytes, as aw-packets hashes a packet file.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The digest.</returns>
    internal static string Sha256(string text) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>A packet id segment: lower-case letters, digits and '-' (aw-packets' PacketId), at most 40 characters.</summary>
    /// <param name="text">Any text.</param>
    /// <returns>The slug, 'x' when nothing is left.</returns>
    internal static string Slug(string text) => PacketSlugs.Slug(text);

    /// <summary>A JSON file's text: the object, one line, and a closing newline.</summary>
    /// <param name="node">The tree.</param>
    /// <returns>The text.</returns>
    internal static string Json(JsonNode node) => LineJson.Write(node) + "\n";

    /// <summary>The object with its 'schemaVersion' first (every line-kind file has it).</summary>
    /// <param name="fields">The fields after it.</param>
    /// <returns>The object.</returns>
    internal static JsonObject Doc(params (string Name, JsonNode? Value)[] fields)
    {
        var obj = new JsonObject { ["schemaVersion"] = 1 };
        foreach (var (name, value) in fields)
        {
            obj[name] = value?.DeepClone();
        }

        return obj;
    }
}
