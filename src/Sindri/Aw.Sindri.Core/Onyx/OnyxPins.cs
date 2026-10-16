using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>
/// The pins of Sindri-managed Onyx: the release, the commit its tag points to, the deployment files and images with
/// their hashes, and what the box needs (context/onyx.lock; spec §6). Parse accepts exactly what scripts/onyx-lock.sh check accepts.
/// </summary>
/// <param name="Version">The release tag, vX.Y.Z.</param>
/// <param name="Commit">The commit the tag points to, 40 lowercase hex digits.</param>
/// <param name="Files">The deployment files to fetch, with their sha256.</param>
/// <param name="Images">Every image for each platform, by digest.</param>
/// <param name="DiskBytes">Free disk the setup needs.</param>
/// <param name="DockerMemoryBytes">The floor for `docker info` MemTotal; the setting the user is told is this rounded up to a whole GiB.</param>
public sealed record OnyxPins(
    string Version, string Commit, IReadOnlyList<OnyxFilePin> Files, IReadOnlyList<OnyxImagePin> Images, long DiskBytes, long DockerMemoryBytes)
{
    private static readonly string[] _platforms = ["linux/arm64", "linux/amd64"];

    /// <summary>Reads the text of a lock file.</summary>
    /// <param name="text">The file's text: `#` comments and blank lines are ignored, the other rows are `version`, `file`, `image` and `need`.</param>
    /// <returns>The pins, or SND-ONYX-004 naming the first row that is malformed.</returns>
    public static Result<OnyxPins> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var reader = new PinsReader();
        var number = 0;
        foreach (var line in text.Split('\n'))
        {
            number++;
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 0 || fields[0].StartsWith('#'))
            {
                continue;
            }

            if (reader.Read(fields) is { } problem)
            {
                return SindriErrors.OnyxPinsMalformed.With($"the Onyx pins are malformed: line {number.ToString(CultureInfo.InvariantCulture)}: {problem}");
            }
        }

        return reader.Build();
    }

    private static bool IsHex(string value, int length) => value.Length == length && value.All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private sealed class PinsReader
    {
        private readonly List<OnyxFilePin> _files = [];
        private readonly List<OnyxImagePin> _images = [];
        private readonly Dictionary<string, long> _needs = new(StringComparer.Ordinal);
        private string? _version;
        private string? _commit;

        public string? Read(string[] fields) => fields[0] switch
        {
            "version" => ReadVersion(fields),
            "file" => ReadFile(fields),
            "image" => ReadImage(fields),
            "need" => ReadNeed(fields),
            var kind => $"unknown row kind \"{kind}\"",
        };

        public Result<OnyxPins> Build()
        {
            var problem = Missing();
            return problem is not null || _version is null || _commit is null
                ? SindriErrors.OnyxPinsMalformed.With($"the Onyx pins are malformed: {problem ?? "no version row"}")
                : new OnyxPins(_version, _commit, _files, _images, _needs["disk-bytes"], _needs["docker-memory-bytes"]);
        }

        private string? Missing()
        {
            if (_version is null)
            {
                return "no version row";
            }

            foreach (var repository in _images.Select(static i => i.Repository).Distinct(StringComparer.Ordinal))
            {
                foreach (var platform in _platforms.Where(p => !_images.Exists(i => i.Repository == repository && i.Platform == p)))
                {
                    return $"{repository} has no {platform} row";
                }
            }

            return _needs.ContainsKey("disk-bytes") ? _needs.ContainsKey("docker-memory-bytes") ? null : "no need docker-memory-bytes row" : "no need disk-bytes row";
        }

        private string? ReadVersion(string[] fields)
        {
            if (_version is not null)
            {
                return "a second version row";
            }

            if (fields.Length != 3 || !IsTag(fields[1]) || !IsHex(fields[2], 40))
            {
                return "version wants a vX.Y.Z tag and a 40-digit lowercase hex commit";
            }

            (_version, _commit) = (fields[1], fields[2]);
            return null;
        }

        private string? ReadFile(string[] fields)
        {
            if (fields.Length != 3 || fields[1].StartsWith('/') || fields[1].Split('/').Contains("..") || !IsHex(fields[2], 64))
            {
                return "file wants a relative path and a 64-digit lowercase hex sha256";
            }

            _files.Add(new OnyxFilePin(fields[1], fields[2]));
            return null;
        }

        private string? ReadImage(string[] fields)
        {
            if (fields.Length != 4 || !_platforms.Contains(fields[2], StringComparer.Ordinal)
                || !fields[3].StartsWith("sha256:", StringComparison.Ordinal) || !IsHex(fields[3][7..], 64))
            {
                return "image wants a repository, linux/arm64 or linux/amd64, and a sha256: digest";
            }

            if (_images.Exists(i => i.Repository == fields[1] && i.Platform == fields[2]))
            {
                return $"{fields[1]} {fields[2]} is listed twice";
            }

            _images.Add(new OnyxImagePin(fields[1], fields[2], fields[3]));
            return null;
        }

        private string? ReadNeed(string[] fields)
        {
            if (fields.Length != 3 || fields[1] is not ("disk-bytes" or "docker-memory-bytes")
                || !long.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) || bytes == 0)
            {
                return "need wants disk-bytes or docker-memory-bytes and a positive integer";
            }

            return _needs.TryAdd(fields[1], bytes) ? null : $"{fields[1]} is listed twice";
        }

        private static bool IsTag(string tag)
        {
            var parts = tag.Length > 1 && tag[0] == 'v' ? tag[1..].Split('.') : [];
            return parts.Length == 3 && parts.All(static p => p.Length > 0 && p.All(char.IsAsciiDigit));
        }
    }
}
