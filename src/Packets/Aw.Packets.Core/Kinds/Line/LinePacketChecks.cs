using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel.Json;
using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>
/// The line kinds' schema checks (LN-PKT1, <see cref="LineRules"/>), one engine for all six: 001 to 006 are shared,
/// then each kind's own, then PKT-BASE-001 (every kind) and, for a plan graph, the build kind's task rules over its
/// tasks.json (LN10). Pure: the directory's files and the base commit's files come in. What the files mean stays in
/// Sindri's DesignPacketChecks, PlanChecks and ProofChecks.
/// </summary>
internal static partial class LinePacketChecks
{
    private const string ManifestFile = PacketReader.ManifestFile;
    private const string IndexFile = "index.json";

    /// <summary>Reads packet.json as the kind's manifest.</summary>
    /// <param name="kind">A line kind.</param>
    /// <param name="files">The packet as read.</param>
    /// <param name="json">The package's JSON options.</param>
    /// <returns>The manifest view, or null when packet.json is missing or doesn't parse (the checks say why).</returns>
    internal static LineManifest? ReadManifest(PacketKind kind, LinePacketFiles files, JsonSerializerOptions json) =>
        Manifest(kind, files, json).Manifest;

    internal static IReadOnlyList<CheckResult> Run(PacketKind kind, LinePacketFiles files, BaseTree? tree, JsonSerializerOptions json)
    {
        var rules = LineRules.For(kind) ?? throw new ArgumentException($"'{kind.Name}' is not a line kind.", nameof(kind));
        var checks = new CheckList();
        var context = new Context(kind, rules, files, json, checks);
        context.Common();
        TaskPlan? plan = null;
        switch (kind.Name)
        {
            case "design":
                context.Design();
                break;
            case "phase":
                context.Phase();
                break;
            case "proof":
                context.Proof();
                break;
            case "handoff":
                context.Handoff();
                break;
            case "plan":
                plan = context.PlanTasks();
                break;
        }

        if (plan is not null && tree is not null && context.Manifest is { } manifest)
        {
            // the task plan is a build packet's, minus the decision log and the out-of-scope list a plan graph doesn't have
            var packet = new Packet(Shell(manifest), new DecisionLog(1, []), new OutOfScope(1, []), plan);
            context.Append(PacketChecks.RunPlan(packet, tree));
        }
        else
        {
            context.Base(tree);
            if (kind.Name == "plan")
            {
                const string Why = "tasks.json can't be read, so PKT's task rules didn't run";
                checks.Unchecked(PacketRules.UniqueIds, Why);
                checks.Unchecked(PacketRules.TaskInWorkstream, Why);
                checks.Unchecked(PacketRules.KnownDependencies, Why);
                checks.Unchecked(PacketRules.Acyclic, Why);
                checks.Unchecked(PacketRules.ScopeExists, Why);
                checks.Unchecked(PacketRules.ScopeOverlap, Why);
                checks.Unchecked(PacketRules.AcceptanceText, Why);
                checks.Unchecked(PacketRules.Evidence, Why);
                checks.Unchecked(PacketRules.Decisions, Why);
                checks.Unchecked(PacketRules.HoldoutExists, Why);
                checks.Unchecked(PacketRules.HoldoutStored, Why);
            }
        }

        return checks.Results;
    }

    private static PacketManifest Shell(LineManifest manifest) => new()
    {
        SchemaVersion = manifest.SchemaVersion,
        Id = manifest.Id,
        Repo = manifest.Repo,
        BaseSha = manifest.BaseSha,
        Status = manifest.Status,
        Hash = manifest.Hash,
        Links = new PacketLinks { Decisions = string.Empty, OutOfScope = string.Empty, Tasks = "tasks.json", Design = [], Plans = [] },
    };

    private static (LineManifest? Manifest, string? Problem) Manifest(PacketKind kind, LinePacketFiles files, JsonSerializerOptions json)
    {
        if (!files.Present.TryGetValue(ManifestFile, out var bytes) || PacketFileShape.Problem(new PacketFileSpec(ManifestFile, PacketFileFormat.Json), bytes) is not null)
        {
            return (null, null);
        }

        var parsed = kind.Name switch
        {
            "design" => AwJson.Deserialize<LinePacketManifest<DesignLinks>>(bytes.Span, json).Map(static m => m.ToView()),
            "plan" => AwJson.Deserialize<LinePacketManifest<PlanLinks>>(bytes.Span, json).Map(static m => m.ToView()),
            "phase" => AwJson.Deserialize<LinePacketManifest<PhaseLinks>>(bytes.Span, json).Map(static m => m.ToView()),
            "proof" => AwJson.Deserialize<LinePacketManifest<ProofLinks>>(bytes.Span, json).Map(static m => m.ToView()),
            "review" => AwJson.Deserialize<LinePacketManifest<ReviewLinks>>(bytes.Span, json).Map(static m => m.ToView()),
            _ => AwJson.Deserialize<LinePacketManifest<HandoffLinks>>(bytes.Span, json).Map(static m => m.ToView()),
        };
        return parsed.IsError ? (null, parsed.Error.Message) : (parsed.Value, null);
    }

    // '<line>-<phase>' or '<line>-<phase>-a<n>': at least two segments, and an amendment suffix is '-a' and a number from 1.
    [GeneratedRegex("^(?<base>[a-z0-9]+(?:-[a-z0-9]+)+?)(?:-a(?<n>[1-9][0-9]*))?\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PhaseIdShape { get; }

    private sealed class Context
    {
        private readonly PacketKind _kind;
        private readonly LineRuleSet _rules;
        private readonly LinePacketFiles _files;
        private readonly JsonSerializerOptions _json;
        private readonly CheckList _checks;
        private readonly HashSet<string> _malformed = new(StringComparer.Ordinal);
        private readonly string? _manifestProblem;

        internal Context(PacketKind kind, LineRuleSet rules, LinePacketFiles files, JsonSerializerOptions json, CheckList checks)
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
            foreach (var m in malformed)
            {
                _ = _malformed.Add(m.Name);
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

        internal TaskPlan? PlanTasks() =>
            Schema<TaskPlan>(_rules.Id(LineRules.TasksSchema), "tasks.json", "task-plan", "tasks.json matches the task-plan schema", static plan => TaskPlanProblems(plan));

        internal void Design()
        {
            Acceptance(LineRules.AcceptanceSchema, allowPassing: false);
        }

        internal void Proof()
        {
            Acceptance(LineRules.AcceptanceSchema, allowPassing: true);
        }

        internal void Handoff()
        {
            _ = Schema<PullRequestFile>(_rules.Id(LineRules.PullRequestSchema), "pr.json", "pull-request", "pr.json matches its schema", PullRequestProblems);
        }

        internal void Phase()
        {
            PhaseId();
            var phase = Schema<PhaseFile>(_rules.Id(LineRules.PhaseSchema), "phase.json", "phase", "phase.json matches its schema", PhaseProblems);
            AmendsRule(phase);
            Acceptance(LineRules.PhaseAcceptanceSchema, allowPassing: false);
            _ = Schema<PhaseHoldout>(_rules.Id(LineRules.PhaseHoldout), "holdout.json", "phase-holdout", "holdout.json matches its schema", HoldoutProblems);
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
                var named = manifest.Links.TryGetValue(spec.Link!, out var paths) ? paths : [];
                if (named.Count != 1 || !string.Equals(named[0], spec.Name, StringComparison.Ordinal))
                {
                    yield return ($"links.{spec.Link} is '{string.Join(", ", named)}'; it names {spec.Name}.", $"Set links.{spec.Link} to \"{spec.Name}\".");
                }
            }

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

                if (named.Distinct(StringComparer.Ordinal).Count() != named.Count)
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

            if (index.Parents.Distinct().Count() != index.Parents.Count)
            {
                yield return ("parents", "parents lists a hash twice.", "List each parent packet once.");
            }
        }

        private void PhaseId()
        {
            var rule = _rules.Id(LineRules.PhaseId);
            if (Manifest is not { } manifest)
            {
                _checks.Unchecked(rule, "packet.json is missing or doesn't match its schema, so the packet id can't be checked");
                return;
            }

            _checks.Rule(
                rule,
                "the packet id is <line>-<phase>[-a<n>]",
                PhaseIdShape.IsMatch(manifest.Id.Value)
                    ? []
                    : [("packet", $"id '{manifest.Id.Value}' is not <line>-<phase> or <line>-<phase>-a<n> (lower-case letters, digits and '-', at least a line and a phase, an amendment number from 1).", "Name the packet <line>-<phase>, and <line>-<phase>-a<n> for the nth amendment. Sindri keeps ids unique per line.")]);
        }

        private void AmendsRule(PhaseFile? phase)
        {
            var rule = _rules.Id(LineRules.PhaseAmends);
            if (Manifest is not { } manifest || phase is null)
            {
                _checks.Unchecked(rule, "packet.json or phase.json can't be read, so amendments can't be checked");
                return;
            }

            var amendment = PhaseIdShape.Match(manifest.Id.Value) is { Success: true } match && match.Groups["n"].Success;
            _checks.Rule(rule, "phase.json has 'amends' exactly when the id ends -a<n>", AmendsProblems(manifest, phase, amendment).Select(static p => ("file phase.json", p.Message, p.Fix)));
        }

        private static IEnumerable<(string Message, string Fix)> AmendsProblems(LineManifest manifest, PhaseFile phase, bool amendment)
        {
            if (amendment && phase.Amends is null)
            {
                yield return ($"id '{manifest.Id.Value}' ends -a<n>, an amendment, but phase.json has no 'amends'.", "Add amends { parent, paths, reason, at }, or drop the suffix.");
            }

            if (!amendment && phase.Amends is not null)
            {
                yield return ($"phase.json has 'amends', but id '{manifest.Id.Value}' doesn't end -a<n>.", "Name an amended packet <line>-<phase>-a<n>.");
            }

            if (phase.Amends is { } amends)
            {
                if (amends.Paths.Count == 0)
                {
                    yield return ("amends.paths is empty; an amendment grants at least one path.", "List the granted paths.");
                }

                foreach (var path in amends.Paths.Where(static p => ScopeGlob.Parse(p) is null))
                {
                    yield return ($"amends.paths '{path}' isn't a glob or a safe 'new: <relative path>'.", "Use the write-scope syntax of allowedFiles.");
                }

                if (string.IsNullOrWhiteSpace(amends.Reason))
                {
                    yield return ("amends.reason is empty.", "Record why the owner allowed the paths.");
                }
            }
        }

        private void Acceptance(int schemaRule, bool allowPassing)
        {
            var schema = _rules.Id(schemaRule);
            var chain = _rules.Id(schemaRule + 1);
            var file = Schema<AcceptanceFile>(schema, "acceptance.json", "acceptance", "acceptance.json matches its schema", static a => AcceptanceShape(a));
            if (file is null)
            {
                _checks.Unchecked(chain, "acceptance.json doesn't match its schema, so its chain rules can't be checked");
                return;
            }

            _checks.Rule(
                chain,
                allowPassing ? "item ids are unique and every passing item has evidence" : "item ids are unique and no item is passing yet",
                ChainProblems(file, allowPassing).Select(static p => ($"acceptance.json {p.Where}", p.Message, p.Fix)));
        }

        private static IEnumerable<(string Where, string Message, string Fix)> AcceptanceShape(AcceptanceFile file)
        {
            if (file.SchemaVersion != PacketReader.SchemaVersion)
            {
                yield return ("schemaVersion", $"schemaVersion is {file.SchemaVersion.ToString(CultureInfo.InvariantCulture)}; this build reads {PacketReader.SchemaVersion.ToString(CultureInfo.InvariantCulture)}.", "Set schemaVersion to 1.");
            }

            for (var i = 0; i < file.Items.Count; i++)
            {
                var item = file.Items[i];
                var where = $"items[{i.ToString(CultureInfo.InvariantCulture)}]";
                if (string.IsNullOrWhiteSpace(item.Id))
                {
                    yield return (where, $"{where} has no id.", "Give every item its id.");
                }

                if (string.IsNullOrWhiteSpace(item.Text))
                {
                    yield return (where, $"{where} has no text.", "Give every item its text; ids and texts never change.");
                }
            }
        }

        private static IEnumerable<(string Where, string Message, string Fix)> ChainProblems(AcceptanceFile file, bool allowPassing)
        {
            foreach (var group in file.Items.GroupBy(static i => i.Id, StringComparer.Ordinal).Where(static g => g.Skip(1).Any()))
            {
                yield return ($"item {group.Key}", $"item '{group.Key}' appears {group.Count().ToString(CultureInfo.InvariantCulture)} times.", "Give every item its own id.");
            }

            foreach (var item in file.Items.Where(static i => i.Status == AcceptanceStatus.Passing))
            {
                if (!allowPassing)
                {
                    yield return ($"item {item.Id}", $"item '{item.Id}' is passing; only the proof pack flips an item to passing (LC-2).", "Set its status to failing.");
                }
                else if (item.Evidence.Count == 0)
                {
                    yield return ($"item {item.Id}", $"item '{item.Id}' is passing with no evidence.", "Add the evidence { kind, path, sha256, commit, at }, or set the status back to failing.");
                }
            }
        }

        private static IEnumerable<(string Where, string Message, string Fix)> TaskPlanProblems(TaskPlan plan)
        {
            if (plan.SchemaVersion != PacketReader.SchemaVersion)
            {
                yield return ("schemaVersion", $"schemaVersion is {plan.SchemaVersion.ToString(CultureInfo.InvariantCulture)}; this build reads {PacketReader.SchemaVersion.ToString(CultureInfo.InvariantCulture)}.", "Set schemaVersion to 1.");
            }

            if (plan.Tasks.Count > PacketReader.MaxTasks)
            {
                yield return ("tasks", $"tasks.json holds {plan.Tasks.Count.ToString(CultureInfo.InvariantCulture)} tasks; a packet holds at most {PacketReader.MaxTasks.ToString(CultureInfo.InvariantCulture)}.", "Split the change.");
            }
        }

        private static IEnumerable<(string Where, string Message, string Fix)> PhaseProblems(PhaseFile phase)
        {
            if (phase.SchemaVersion != PacketReader.SchemaVersion)
            {
                yield return ("schemaVersion", $"schemaVersion is {phase.SchemaVersion.ToString(CultureInfo.InvariantCulture)}; this build reads {PacketReader.SchemaVersion.ToString(CultureInfo.InvariantCulture)}.", "Set schemaVersion to 1.");
            }

            if (string.IsNullOrWhiteSpace(phase.Goal))
            {
                yield return ("goal", "goal is empty.", "State what the phase achieves.");
            }

            if (string.IsNullOrWhiteSpace(phase.Base.Ref))
            {
                yield return ("base.ref", "base.ref is empty.", "Name the branch (default-branch) or the dependency phase (dependency-head).");
            }

            if (phase.Base.Rule == BaseRule.DependencyHead && !phase.Deps.Contains(phase.Base.Ref, StringComparer.Ordinal))
            {
                yield return ("base.ref", $"base.ref '{phase.Base.Ref}' is not one of deps, but the rule is dependency-head.", "Point base.ref at a phase in deps.");
            }

            if (phase.Deps.Distinct(StringComparer.Ordinal).Count() != phase.Deps.Count)
            {
                yield return ("deps", "deps lists a phase twice.", "List each dependency once.");
            }

            if (phase.AllowedFiles.Count == 0)
            {
                yield return ("allowedFiles", "allowedFiles is empty; a phase that may write nothing builds nothing.", "List the globs and 'new: <path>' entries the phase may write.");
            }

            foreach (var entry in phase.AllowedFiles.Where(static e => ScopeGlob.Parse(e) is null))
            {
                yield return ("allowedFiles", $"allowedFiles '{entry}' isn't a glob or a safe 'new: <relative path>'.", "Use a relative glob, or 'new: <path>' for a file the phase creates.");
            }

            foreach (var change in phase.TestChange.Where(static t => string.IsNullOrWhiteSpace(t.Path) || string.IsNullOrWhiteSpace(t.Reason)))
            {
                yield return ("testChange", $"testChange '{change.Path}' needs a path and a reason.", "Name the existing test file and why it changes (BLD-TEST-001).");
            }
        }

        private static IEnumerable<(string Where, string Message, string Fix)> HoldoutProblems(PhaseHoldout holdout)
        {
            if (holdout.SchemaVersion != PacketReader.SchemaVersion)
            {
                yield return ("schemaVersion", $"schemaVersion is {holdout.SchemaVersion.ToString(CultureInfo.InvariantCulture)}; this build reads {PacketReader.SchemaVersion.ToString(CultureInfo.InvariantCulture)}.", "Set schemaVersion to 1.");
            }

            if (holdout.Count < 0)
            {
                yield return ("count", "count is negative.", "Record how many scenarios the set holds.");
            }
        }

        private static IEnumerable<(string Where, string Message, string Fix)> PullRequestProblems(PullRequestFile pr)
        {
            if (pr.SchemaVersion != PacketReader.SchemaVersion)
            {
                yield return ("schemaVersion", $"schemaVersion is {pr.SchemaVersion.ToString(CultureInfo.InvariantCulture)}; this build reads {PacketReader.SchemaVersion.ToString(CultureInfo.InvariantCulture)}.", "Set schemaVersion to 1.");
            }

            foreach (var (name, value) in new[] { ("base", pr.Base), ("headBranch", pr.HeadBranch), ("title", pr.Title) })
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    yield return (name, $"{name} is empty.", $"Record the PR's {name}.");
                }
            }

            if (pr.Labels.Distinct(StringComparer.Ordinal).Count() != pr.Labels.Count)
            {
                yield return ("labels", "labels lists a label twice.", "List each label once.");
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
}
