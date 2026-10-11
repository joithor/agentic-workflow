using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.BugFix.Infrastructure;

/// <summary>Run records under &lt;state&gt;/runs/ and judge inputs beside state.json, as JSON files.</summary>
internal sealed class FileRunLedger(JsonSerializerOptions json) : IRunLedger
{
    public RunSlot Reserve(string directory, DateTimeOffset at, CheckKind kind)
    {
        var runs = Directory.CreateDirectory(Path.Combine(directory, "runs")).FullName;
        var stamp = at.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
        var name = kind == CheckKind.Test ? "test" : "ui";
        for (var n = 1; ; n++)
        {
            var stem = Path.Combine(runs, string.Create(CultureInfo.InvariantCulture, $"{stamp}-{name}-{n}"));
            try
            {
                // CreateNew claims the slot, so two runs in the same millisecond never share files.
                using (new FileStream($"{stem}.json", FileMode.CreateNew))
                {
                }
            }
            catch (IOException) when (File.Exists($"{stem}.json"))
            {
                continue;
            }

            return new RunSlot(kind == CheckKind.Test ? $"{stem}.log" : stem, $"{stem}.log", $"{stem}.json", DigestKey(directory));
        }
    }

    // A random key per bug fix in digest.key (owner-only), made on first use; two runs racing to make it read the winner's.
    private static string DigestKey(string directory)
    {
        var path = Path.Combine(directory, "digest.key");
        if (!File.Exists(path))
        {
            try
            {
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
                if (!OperatingSystem.IsWindows())
                {
                    options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                }

                using var stream = new FileStream(path, options);
                stream.Write(Encoding.ASCII.GetBytes(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32))));
            }
            catch (IOException) when (File.Exists(path))
            {
                // another run made it first
            }
        }

        return File.ReadAllText(path).Trim();
    }

    public string Write(RunSlot slot, RunRecord record) => WriteFile(slot.Record, AwJson.Serialize(record, json));

    public Result<StoredRun> Read(string path)
    {
        var full = Path.GetFullPath(path);
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return BugFixErrors.EvidenceRefused.With($"cannot read evidence: {full}");
        }

        var record = AwJson.Deserialize<RunRecord>(bytes, json);
        return record.IsError
            ? BugFixErrors.EvidenceRefused.With($"{full} is not a bugfix-state run record (write evidence only with run-test or run-ui)")
            : new StoredRun(full, Convert.ToHexStringLower(SHA256.HashData(bytes)), record.Value);
    }

    public void Release(RunSlot slot) => File.Delete(slot.Record);

    public JudgeQuestion WriteJudgeInput(string directory, CandidateId candidate, ResolutionCheckInput input)
    {
        var file = Path.Combine(directory, $"judge-{candidate.Value}.json");
        return new JudgeQuestion(input, file, WriteFile(file, AwJson.Serialize(input, json)));
    }

    private static string WriteFile(string path, byte[] bytes)
    {
        File.WriteAllBytes(path, bytes);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
