using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Packets.Core.Checks;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>One line packet's check run: the shared rules 001 to 006 and the helpers the kind rules use.</summary>
internal sealed partial class LineContext
{
    private const string ManifestFile = PacketReader.ManifestFile;
    private const string IndexFile = "index.json";

    private readonly PacketKind _kind;
    private readonly LineRuleSet _rules;
    private readonly LinePacketFiles _files;
    private readonly JsonSerializerOptions _json;
    private readonly CheckList _checks;
    private readonly HashSet<string> _malformed = new(StringComparer.Ordinal);
    private readonly string? _manifestProblem;

    internal LineContext(PacketKind kind, LineRuleSet rules, LinePacketFiles files, JsonSerializerOptions json, CheckList checks)
    {
        _kind = kind;
        _rules = rules;
        _files = files;
        _json = json;
        _checks = checks;
        (Manifest, _manifestProblem) = LinePacketChecks.Manifest(kind, files, json);
    }

    internal LineManifest? Manifest { get; }

    internal void Append(IEnumerable<CheckResult> results) => _checks.AddRange(results);

    internal void Common()
    {
        _checks.Rule(
            _rules.FilesPresent,
            $"all {_kind.Files.Count.ToString(CultureInfo.InvariantCulture)} {_kind.Name} files are present",
            _kind.Files.Where(f => !_files.Present.ContainsKey(f.Name)).Select(static f => (
                $"file {f.Name}",
                $"{f.Name} is missing.",
                $"Write {f.Name} into the packet directory.")));
        _checks.Rule(
            _rules.NoUnlisted,
            $"the directory holds only the {_kind.Name} files",
            _files.Unlisted.Select(name => (
                $"entry {name}",
                $"{name} is not a file of the {_kind.Name} kind.",
                $"Remove {name} from the packet directory.")));
        var malformed = WellFormed().ToList();
        foreach (var (Name, Subject, Message, Fix) in malformed)
        {
            _ = _malformed.Add(Name);
        }

        _checks.Rule(_rules.WellFormed, "every file is well formed", malformed.Select(static m => (m.Subject, m.Message, m.Fix)));
        ManifestRule();
        LinksRule();
        IndexRule();
    }

    internal void Base(BaseTree? tree)
    {
        if (tree is null)
        {
            _checks.Unchecked(PacketRules.BaseCommit, "packet.json is missing or doesn't match its schema, so its base commit is unknown");
            return;
        }

        _checks.Rule(
            PacketRules.BaseCommit,
            $"base commit {Short(Manifest?.BaseSha)} is in the repository",
            tree.Problem is { } problem ? [("packet", problem, "Set baseSha to a full commit sha that exists in the repository.")] : []);
    }

    private IEnumerable<(string Name, string Subject, string Message, string Fix)> WellFormed()
    {
        foreach (var spec in _kind.Files)
        {
            if (_files.Present.TryGetValue(spec.Name, out var bytes) && PacketFileShape.Problem(spec, bytes) is { } message)
            {
                yield return (spec.Name, $"file {spec.Name}", message, PacketFileShape.Fix(spec));
            }
        }
    }

    private void ManifestRule()
    {
        var rule = _rules.Manifest;
        if (!_files.Present.ContainsKey(ManifestFile) || _malformed.Contains(ManifestFile))
        {
            _checks.Unchecked(rule, "packet.json is missing or not well formed, so its schema can't be checked");
            return;
        }

        if (Manifest is not { } manifest)
        {
            _checks.Rule(rule, string.Empty, [($"file {ManifestFile}", _manifestProblem ?? "packet.json does not match the schema.", $"Fix packet.json to match schemas/packets/{_kind.Name}-packet.schema.json.")]);
            return;
        }

        _checks.Rule(rule, $"packet.json matches the {_kind.Name} schema", ManifestProblems(manifest).Select(static p => ($"file {ManifestFile}", p.Message, p.Fix)));
    }

    private IEnumerable<(string Message, string Fix)> ManifestProblems(LineManifest manifest)
    {
        if (manifest.SchemaVersion != PacketReader.SchemaVersion)
        {
            yield return ($"schemaVersion is {manifest.SchemaVersion.ToString(CultureInfo.InvariantCulture)}; this build reads {PacketReader.SchemaVersion.ToString(CultureInfo.InvariantCulture)}.", "Set schemaVersion to 1.");
        }

        if (!string.Equals(manifest.Kind, _kind.Name, StringComparison.Ordinal))
        {
            yield return ($"kind is '{manifest.Kind}'; this is a {_kind.Name} packet.", $"Set kind to '{_kind.Name}'.");
        }
    }

    private void LinksRule()
    {
        var rule = _rules.Links;
        if (Manifest is not { } manifest)
        {
            _checks.Unchecked(rule, "packet.json is missing or doesn't match its schema, so its links can't be checked");
            return;
        }

        _checks.Rule(rule, "links name each file of the kind, and index.json", LinkProblems(manifest).Select(static p => ("links", p.Message, p.Fix)));
    }

    private IEnumerable<(string Message, string Fix)> LinkProblems(LineManifest manifest)
    {
        foreach (var spec in _kind.Files.Where(static f => f.Link is not null))
        {
            var named = manifest.Links.TryGetValue(spec.Link ?? string.Empty, out var paths) ? paths : [];
            if (named.Count != 1 || !string.Equals(named[0], spec.Name, StringComparison.Ordinal))
            {
                yield return ($"links.{spec.Link} is '{string.Join(", ", named)}'; it names {spec.Name}.", $"Set links.{spec.Link} to \"{spec.Name}\".");
            }
        }

        foreach (var problem in DirectoryProblems(manifest))
        {
            yield return problem;
        }
    }

    private IEnumerable<(string Message, string Fix)> DirectoryProblems(LineManifest manifest)
    {
        foreach (var directory in _kind.Directories)
        {
            var named = manifest.Links.TryGetValue(directory.Link, out var paths) ? paths : [];
            var actual = _files.Present.Keys.Where(k => k.StartsWith(directory.Prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList();
            foreach (var path in actual.Except(named, StringComparer.Ordinal))
            {
                yield return ($"{path} is in {directory.Prefix} but links.{directory.Link} doesn't list it, so the hash would not cover it.", $"Add \"{path}\" to links.{directory.Link}.");
            }

            foreach (var path in named.Except(actual, StringComparer.Ordinal))
            {
                yield return ($"links.{directory.Link} lists '{path}', which is not a file under {directory.Prefix}.", $"Remove it from links.{directory.Link}, or add the file under {directory.Prefix}.");
            }

            if (LineProblems.Repeats(named))
            {
                yield return ($"links.{directory.Link} lists a path twice.", "List each file once.");
            }
        }
    }

    private void IndexRule()
    {
        var rule = _rules.Index;
        _ = Schema<PacketIndex>(rule, IndexFile, "index", "index.json matches its schema and says the kind", IndexProblems);
    }

    private IEnumerable<(string Where, string Message, string Fix)> IndexProblems(PacketIndex index)
    {
        if (index.SchemaVersion != PacketReader.SchemaVersion)
        {
            yield return ("schemaVersion", $"schemaVersion is {index.SchemaVersion.ToString(CultureInfo.InvariantCulture)}; this build reads {PacketReader.SchemaVersion.ToString(CultureInfo.InvariantCulture)}.", "Set schemaVersion to 1.");
        }

        if (!string.Equals(index.Kind, _kind.Name, StringComparison.Ordinal))
        {
            yield return ("kind", $"kind is '{index.Kind}'; this is a {_kind.Name} packet.", $"Set kind to '{_kind.Name}'.");
        }

        if (LineProblems.Repeats(index.Parents))
        {
            yield return ("parents", "parents lists a hash twice.", "List each parent packet once.");
        }
    }

    // Reads one typed file; null (and the rule failed or unchecked) when it is missing, malformed or doesn't match.
    private T? Schema<T>(string rule, string file, string schema, string pass, Func<T, IEnumerable<(string Where, string Message, string Fix)>> problems)
        where T : class
    {
        if (!_files.Present.TryGetValue(file, out var bytes) || _malformed.Contains(file))
        {
            _checks.Unchecked(rule, $"{file} is missing or not well formed, so its schema can't be checked");
            return null;
        }

        var parsed = AwJson.Deserialize<T>(bytes.Span, _json);
        if (parsed.IsError)
        {
            _checks.Rule(rule, string.Empty, [($"file {file}", parsed.Error.Message, $"Fix {file} to match schemas/packets/{schema}.schema.json.")]);
            return null;
        }

        var found = problems(parsed.Value).ToList();
        _checks.Rule(rule, pass, found.Select(f => ($"{file} {f.Where}", f.Message, f.Fix)));
        return found.Count == 0 ? parsed.Value : null;
    }

    private static string Short(string? sha) => sha is { Length: > 12 } ? sha[..12] : sha ?? "?";
}
