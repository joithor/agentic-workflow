using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.LensPacks;

namespace Aw.Sindri.Tests.LensPacks;

/// <summary>The style packs (line spec §5.1): the built-in 'plain' pack read through the embedded source, which pack the profile chooses, and what a pack.json must hold.</summary>
[Lane(TestLane.Unit)]
public sealed class StylePacksTests
{
#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string PackJson = """
        {"kind":"style","id":"mini","version":"1.0.0","title":"Mini",
         "sections":[{"id":"summary","heading":"Summary","required":true,"guidance":"Short.","maxWords":50},{"id":"notes","heading":"Notes","required":false,"guidance":"Optional."}],
         "rules":{"forbidden":["shall","in order to"],"maxLines":40,"tables":"allow","voice":"voice.md"},
         "template":"template.md","examples":["examples/a.md"]}
        """;
#pragma warning restore MA0136

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Dictionary<string, string> Files(Func<string, string>? edit = null) => new(StringComparer.Ordinal)
    {
        ["pack.json"] = edit is null ? PackJson : edit(PackJson),
        ["template.md"] = "# t\n",
        ["voice.md"] = "Plain.\n",
        ["examples/a.md"] = "An example.\n",
    };

    private static string Problem(Func<string, string> edit)
    {
        var read = StylePackReader.Read("mini", Files(edit));

        Assert.True(read.IsError);
        return read.Error.Message;
    }

    [Fact]
    public async Task With_nothing_chosen_the_profile_resolves_the_built_in_plain_pack()
    {
        var packs = new ProfileStylePacks(new NoProfile(), new EmbeddedStylePackSource());

        var plain = await packs.Resolve(repo: null, Token);

        Assert.Equal("plain", plain.Value.Id.Value);
        Assert.Equal(["Summary", "Behaviour", "Data", "Proof", "Rows to other lanes"], plain.Value.Sections.Select(static s => s.Heading), StringComparer.Ordinal);
        Assert.All(plain.Value.Sections, static s => Assert.True(s.Required));
        Assert.Equal(100, plain.Value.Sections[0].MaxWords);
        Assert.Contains("shall", plain.Value.Rules.Forbidden, StringComparer.Ordinal);
        Assert.Equal(120, plain.Value.Rules.MaxLines);
        Assert.Matches("^sha256:[0-9a-f]{64}$", plain.Value.Hash);
    }

    [Fact]
    public void The_embedded_source_holds_plain_and_Find_reads_it_by_id()
    {
        var packs = new ProfileStylePacks(new NoProfile(), new EmbeddedStylePackSource());

        Assert.Equal(["plain"], new EmbeddedStylePackSource().Ids(), StringComparer.Ordinal);
        Assert.Equal("plain", packs.Find(LensPackId.From("plain")).Value.Id.Value);
    }

    [Fact]
    public void An_id_that_is_not_a_built_in_pack_is_an_error_naming_the_built_in_ones()
    {
        var found = new ProfileStylePacks(new NoProfile(), new EmbeddedStylePackSource()).Find(LensPackId.From("formal"));

        Assert.True(found.IsError);
        Assert.Contains("plain", found.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_repo_entry_wins_over_the_default_and_an_unknown_choice_is_an_error_never_a_fallback()
    {
        var choice = new PackChoice("plain", new Dictionary<string, string>(StringComparer.Ordinal) { ["web"] = "formal" });
        var packs = new ProfileStylePacks(new NoProfile(choice), new EmbeddedStylePackSource());

        var other = await packs.Resolve(RepoName.From("api"), Token);
        var web = await packs.Resolve(RepoName.From("web"), Token);

        Assert.Equal("plain", other.Value.Id.Value);
        Assert.True(web.IsError);
        Assert.Contains("formal", web.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_valid_pack_reads_its_sections_in_order_and_its_rules()
    {
        var pack = StylePackReader.Read("mini", Files()).Value;

        Assert.Equal("mini", pack.Id.Value);
        Assert.Equal(["Summary", "Notes"], pack.Sections.Select(static s => s.Heading), StringComparer.Ordinal);
        Assert.Equal([true, false], pack.Sections.Select(static s => s.Required));
        Assert.Equal(50, pack.Sections[0].MaxWords);
        Assert.Null(pack.Sections[1].MaxWords);
        Assert.Equal(["shall", "in order to"], pack.Rules.Forbidden, StringComparer.Ordinal);
        Assert.Equal(40, pack.Rules.MaxLines);
    }

    [Fact]
    public void The_hash_follows_the_files_and_not_their_order()
    {
        var files = Files();
        var reordered = new Dictionary<string, string>(files.Reverse(), StringComparer.Ordinal);
        var edited = Files();
        edited["voice.md"] = "Plainer.\n";

        var hash = StylePackReader.Read("mini", files).Value.Hash;

        Assert.Equal(hash, StylePackReader.Read("mini", reordered).Value.Hash, StringComparer.Ordinal);
        Assert.NotEqual(hash, StylePackReader.Read("mini", edited).Value.Hash, StringComparer.Ordinal);
    }

    [Fact]
    public void A_pack_without_a_pack_json_is_refused()
    {
        var files = Files();
        _ = files.Remove("pack.json");

        Assert.True(StylePackReader.Read("mini", files).IsError);
    }

    [Fact]
    public void A_pack_json_of_another_kind_or_another_id_is_refused()
    {
        Assert.Contains("style pack", Problem(static j => j.Replace("\"kind\":\"style\"", "\"kind\":\"lenses\"", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Contains("'mini'", Problem(static j => j.Replace("\"id\":\"mini\"", "\"id\":\"other\"", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_pack_json_that_is_not_json_is_refused()
        => Assert.Contains("not JSON", Problem(static _ => "{"), StringComparison.Ordinal);

    [Fact]
    public void A_pack_with_no_section_or_a_section_without_a_heading_is_refused()
    {
        Assert.Contains("no section", Problem(static j => j[..j.IndexOf("\"sections\"", StringComparison.Ordinal)] + "\"sections\":[]," + j[j.IndexOf("\"rules\"", StringComparison.Ordinal)..]), StringComparison.Ordinal);
        Assert.Contains("heading", Problem(static j => j.Replace("\"heading\":\"Notes\",", string.Empty, StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_heading_named_twice_is_refused()
        => Assert.Contains("twice", Problem(static j => j.Replace("\"heading\":\"Notes\"", "\"heading\":\"Summary\"", StringComparison.Ordinal)), StringComparison.Ordinal);

    [Fact]
    public void A_section_whose_required_flag_is_not_a_boolean_or_whose_limit_is_not_a_positive_number_is_refused()
    {
        Assert.Contains("required", Problem(static j => j.Replace("\"required\":true", "\"required\":\"yes\"", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Contains("maxWords", Problem(static j => j.Replace("\"maxWords\":50", "\"maxWords\":0", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_template_voice_or_example_the_pack_names_but_does_not_hold_is_refused()
    {
        var withoutTemplate = Files();
        _ = withoutTemplate.Remove("template.md");
        var withoutVoice = Files();
        _ = withoutVoice.Remove("voice.md");
        var withoutExample = Files();
        _ = withoutExample.Remove("examples/a.md");

        Assert.Contains("template.md", StylePackReader.Read("mini", withoutTemplate).Error.Message, StringComparison.Ordinal);
        Assert.Contains("voice.md", StylePackReader.Read("mini", withoutVoice).Error.Message, StringComparison.Ordinal);
        Assert.Contains("examples/a.md", StylePackReader.Read("mini", withoutExample).Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_forbidden_list_with_a_blank_entry_or_a_tables_rule_other_than_prefer_or_allow_is_refused()
    {
        Assert.Contains("forbidden", Problem(static j => j.Replace("\"shall\"", "\"\"", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Contains("tables", Problem(static j => j.Replace("\"tables\":\"allow\"", "\"tables\":\"never\"", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    private sealed class NoProfile(PackChoice? choose = null) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken)
        {
            var profile = new SindriProfile(1, "t") { Workflow = new WorkflowSettings(Packs: new PackSettings(Style: choose)) };
            return ValueTask.FromResult<Result<LoadedProfile?>>(new LoadedProfile(profile, ProfileHash.From(new string('a', 64)), "/profile.yaml"));
        }
    }
}
