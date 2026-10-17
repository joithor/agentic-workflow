using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;

namespace Aw.Context.Tests.Map;

[Trait("Lane", "unit")]
public sealed class MapVersionRefTests
{
    [Theory]
    [InlineData("3", 3, null)]
    [InlineData("v3", 3, null)]
    [InlineData("v12@a41b07d", 12, "a41b07d")]
    [InlineData("V7", 7, null)]
    public void N_vN_and_vN_at_commit_parse(string text, int number, string? commit)
    {
        var parsed = MapVersionRef.Parse(text);

        Assert.True(parsed.IsSuccess);
        Assert.Equal((number, commit), (parsed.Value.Number, parsed.Value.Commit));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("v")]
    [InlineData("v0")]
    [InlineData("-1")]
    [InlineData("v1.5")]
    [InlineData("v3@")]
    [InlineData("v3@zz!")]
    [InlineData("latest")]
    [InlineData("v99999999999")]
    public void Anything_else_is_006_naming_the_value(string text)
    {
        var parsed = MapVersionRef.Parse(text);

        Assert.Equal(ContextErrors.QueryInvalid.Code, parsed.Error.Code);
    }

    [Fact]
    public void A_null_is_006() =>
        Assert.Equal(ContextErrors.QueryInvalid.Code, MapVersionRef.Parse(null).Error.Code);

    [Fact]
    public void A_commit_part_names_only_a_version_whose_commit_starts_with_it()
    {
        var stored = new MapVersion(4, ContextRepoName.From("aw-split-panes"), "a41b07d9c0ffee", KeyVersion: 1, DateTimeOffset.UnixEpoch, MapVersionStatus.Retained);

        Assert.True(new MapVersionRef(4, Commit: null).Names(stored));
        Assert.True(new MapVersionRef(4, "a41b07d").Names(stored));
        Assert.False(new MapVersionRef(4, "b41b07d").Names(stored));
        Assert.False(new MapVersionRef(5, Commit: null).Names(stored));
        Assert.False(new MapVersionRef(4, "a41b07d").Names(stored with { Commit = null }));
    }

    [Fact]
    public void Draft_together_with_a_version_is_two_shapes_and_006()
    {
        Assert.Equal(ContextErrors.QueryInvalid.Code, MapRead.Of(draft: true, "v1").Error.Code);
        Assert.Equal(ContextErrors.QueryInvalid.Code, MapRead.Of(draft: false, "v1x").Error.Code);
        Assert.Equal(MapRead.Current, MapRead.Of(draft: false, mapVersion: null).Value);
        Assert.Equal(MapRead.NewestDraft, MapRead.Of(draft: true, mapVersion: null).Value);
        Assert.Equal(new MapVersionRef(1, Commit: null), MapRead.Of(draft: false, "v1").Value.Version);
    }
}
