using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Host;
using Aw.BugFix.Infrastructure;
using Aw.BugFix.Testing;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Contracts;

[Lane(TestLane.Integration)]
public sealed class FileRunLedgerContractTests : RunLedgerContract, IDisposable
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(BugFixJsonContext.Default);
    private readonly TempDirectory _temp = new();

    protected override IRunLedger Ledger { get; } = new FileRunLedger(_json);

    protected override string Directory => _temp.Path;

    protected override string OtherDirectory => System.IO.Directory.CreateDirectory(Path.Combine(_temp.Path, "other")).FullName;

    [Theory]
    [InlineData("")]
    [InlineData("not hex at all")]
    [InlineData("abcd")]
    public void A_malformed_digest_key_is_AW_BUGFIX_023_never_a_crash_or_a_new_key(string content)
    {
        File.WriteAllText(Path.Combine(_temp.Path, "digest.key"), content);

        Assert.Equal(Core.Errors.BugFixErrors.DigestKeyMissing.Code, Ledger.Reserve(_temp.Path, DateTimeOffset.UnixEpoch, CheckKind.Test).Error.Code);

        Ledger.CreateDigestKey(_temp.Path);   // init replaces it

        Assert.Matches("^[0-9a-f]{64}$", Ledger.Reserve(_temp.Path, DateTimeOffset.UnixEpoch, CheckKind.Test).Value.DigestKey);
    }

    public void Dispose() => _temp.Dispose();

    protected override void Edit(string path, RunRecord record) => File.WriteAllBytes(path, AwJson.Serialize(record, _json));
}
