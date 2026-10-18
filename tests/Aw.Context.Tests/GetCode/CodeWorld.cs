using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Testing.Code;
using Aw.Context.Tests.Map;
using Aw.Kernel;

namespace Aw.Context.Tests.GetCode;

/// <summary>A repo with implementing files, test files and schema files, a map over it and the code lens asked of it.</summary>
internal sealed class CodeWorld : IDisposable
{
    internal const string Anchor = "feature:sharing";

    internal static IReadOnlyDictionary<string, string> Files { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["src/sharing/links.ts"] = "export const sharedLink = 1; // table shared_link",
        ["src/sharing/routes.ts"] = "export const routes = [];",
        ["src/unrelated.ts"] = "export const nothing = 0;",
        ["tests/links.test.ts"] = "import { sharedLink } from '../src/sharing/links';",
        ["tests/routes.test.ts"] = "describe('routes', () => {});",
        ["tests/edge.test.ts"] = "import { sharedLink } from '../src/sharing/links';",
        ["tests/other.test.ts"] = "import { x } from '../src/sharing/links-extra';",
        ["migrations/002_sharing.sql"] = "create table shared_link (id int);",
        ["migrations/003_other.sql"] = "create table other (id int);",
        [".env"] = "TOKEN=hunter2 shared_link",
    };

    private const string Header = "schemaVersion: 1\nkind: authored\nrepo: aw-split-panes\n";

    internal CodeWorld(IReadOnlyDictionary<string, string>? files = null, Func<ICodeSearch, ICodeSearch>? decorate = null)
    {
        var plain = new FakeCodeSearch(MapWorld.Repos[0], MapWorld.Head, files ?? Files);
        Search = new RecordingCodeSearch(decorate is null ? plain : decorate(plain));
        World = new MapWorld(new Aw.Context.Testing.Extract.FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, files ?? Files), intent: null, codeSearch: Search);
    }

    internal MapWorld World { get; }

    internal RecordingCodeSearch Search { get; }

    public void Dispose() => World.Dispose();

    /// <summary>The sharing map: two implementing files, one edge test, a thing the feature writes.</summary>
    internal Task<Aw.Context.Core.Map.MapImported> ImportSharing(string? commit = null, string[]? extraNodes = null, string[]? extraEdges = null) =>
        World.ImportYaml(
            (commit is null ? string.Empty : $"commit: {commit}\n") + Header + "nodes:\n" + string.Join('\n',
            [
                "  - {key: \"feature:sharing\", label: \"Sharing\"}",
                "  - {key: \"surface:screen/share\", label: \"Share\"}",
                "  - {key: \"action:sharing.create\", label: \"Create a link\"}",
                "  - {key: \"thing:shared-link\", label: \"Shared link\"}",
                "  - {key: \"code:src/sharing/links.ts\", label: \"links.ts\", provenance: deterministic}",
                "  - {key: \"code:src/sharing/routes.ts\", label: \"routes.ts\", provenance: deterministic}",
                "  - {key: \"code:tests/edge.test.ts\", label: \"edge.test.ts\", provenance: deterministic}",
                .. extraNodes ?? [],
            ]) + "\nedges:\n" + string.Join('\n',
            [
                "  - {from: \"surface:screen/share\", to: \"feature:sharing\", type: part-of}",
                "  - {from: \"action:sharing.create\", to: \"surface:screen/share\", type: on}",
                "  - {from: \"action:sharing.create\", to: \"thing:shared-link\", type: writes}",
                "  - {from: \"code:src/sharing/links.ts\", to: \"feature:sharing\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/sharing/links.ts\"}]}",
                "  - {from: \"code:src/sharing/routes.ts\", to: \"feature:sharing\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/sharing/routes.ts\"}]}",
                "  - {from: \"code:tests/edge.test.ts\", to: \"feature:sharing\", type: tests, provenance: deterministic}",
                .. extraEdges ?? [],
            ]) + "\n");

    internal ValueTask<Result<CodeView>> Code(ContextScope? scope = null, string[]? anchors = null, bool draft = false, string? mapVersion = null) =>
        World.Engine.Code(anchors ?? [Anchor], draft, mapVersion, scope ?? MapWorld.Human, MapWorld.None);
}
