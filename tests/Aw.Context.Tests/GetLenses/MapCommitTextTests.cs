using Aw.Context.Core.Map;

namespace Aw.Context.Tests.GetLenses;

public sealed class MapCommitTextTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("0A1B2C3D4E5F60718293A4B5C6D7E8F901234567", "0a1b2c3")]
    [InlineData("a41b07d", "a41b07d")]
    [InlineData("A41B07D9", "a41b07d")]
    [InlineData("a41b0", null)]
    [InlineData("not-a-sha-at-all", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void MapCommit_is_seven_lowercase_hex_characters_or_omitted(string? commit, string? expected) =>
        Assert.Equal(expected, LensBasisReader.ShortCommit(commit));
}
