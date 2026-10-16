using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Tests.OnyxPinsTests;

public sealed class OnyxPinsParseTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private static readonly string _sha = new('a', 64);
    private static readonly string _digest = "sha256:" + new string('b', 64);

    private static string Fixture(string extra = "") => string.Join(
        '\n',
        "# a comment",
        "",
        $"version v4.9.0 {Commit}",
        $"file docker_compose/docker-compose.yml {_sha}",
        $"image docker.io/o/app linux/arm64 {_digest}",
        $"image docker.io/o/app linux/amd64 {_digest}",
        "need disk-bytes 100",
        "need docker-memory-bytes 200",
        extra);

    [Fact, Trait("Lane", "unit")]
    public void Parse_reads_every_row_kind()
    {
        var pins = OnyxPins.Parse(Fixture()).Value;

        Assert.Equal("v4.9.0", pins.Version);
        Assert.Equal(Commit, pins.Commit);
        Assert.Equal([new OnyxFilePin("docker_compose/docker-compose.yml", _sha)], pins.Files);
        Assert.Equal(
            [new OnyxImagePin("docker.io/o/app", "linux/arm64", _digest), new OnyxImagePin("docker.io/o/app", "linux/amd64", _digest)],
            pins.Images);
        Assert.Equal(100, pins.DiskBytes);
        Assert.Equal(200, pins.DockerMemoryBytes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Parse_refuses_a_missing_version_row()
    {
        var result = OnyxPins.Parse(Fixture().Replace($"version v4.9.0 {Commit}", "", StringComparison.Ordinal));

        Assert.Equal(SindriErrors.OnyxPinsMalformed.Code, result.Error.Code);
        Assert.Contains("no version row", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("version 4.9 0123456789abcdef0123456789abcdef01234567")]
    [InlineData("version v4.9.0 notacommit")]
    [InlineData("frobnicate x")]
    [InlineData("file /abs/path aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("image docker.io/o/app linux/arm64 sha256:abc")]
    [InlineData("need disk-bytes lots")]
    [InlineData("need cpu-cores 4")]
    public void Parse_refuses_a_row_the_bash_check_would_refuse(string row)
    {
        var result = OnyxPins.Parse(Fixture(row));

        Assert.Equal(SindriErrors.OnyxPinsMalformed.Code, result.Error.Code);
        Assert.StartsWith("the Onyx pins are malformed: ", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Parse_refuses_an_image_missing_a_platform()
    {
        var result = OnyxPins.Parse(Fixture().Replace($"image docker.io/o/app linux/amd64 {_digest}", "", StringComparison.Ordinal));

        Assert.Equal(SindriErrors.OnyxPinsMalformed.Code, result.Error.Code);
        Assert.Contains("docker.io/o/app has no linux/amd64 row", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Parse_refuses_a_second_version_row()
        => Assert.True(OnyxPins.Parse(Fixture($"version v4.9.1 {Commit}")).IsError);

    [Fact, Trait("Lane", "unit")]
    public void Parse_refuses_a_missing_need_row()
        => Assert.True(OnyxPins.Parse(Fixture().Replace("need docker-memory-bytes 200", "", StringComparison.Ordinal)).IsError);
}
