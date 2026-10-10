using System.Globalization;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Testing;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class InMemoryReposContractTests : ReposContract
{
    private readonly InMemoryRepos _repos = new();
    private int _next;

    protected override IGit Git => _repos;

    protected override IFiles Files => _repos;

    protected override string Outside => "/nowhere";

    protected override (string Root, string Commit) NewRepo()
    {
        var root = string.Create(CultureInfo.InvariantCulture, $"/r{++_next}");
        return (root, _repos.Init(root, Base));
    }

    protected override void Write(string root, string path, string text) => _repos.Write($"{root}/{path}", text);

    protected override string Commit(string root) => _repos.Commit(root);

    internal static Dictionary<string, string> Base => new(StringComparer.Ordinal) { ["src/a.txt"] = "a\n", ["tests/t.sh"] = "exit 0\n" };
}
