using System.Text.Json;
using Aw.Kernel;

namespace Aw.Evidence.Core.Desktop;

/// <summary>Reads a screens manifest and checks its rules (the red start: the behavior arrives with the next commit).</summary>
public static class DesktopManifestReader
{
    /// <summary>Parses and validates a screens manifest.</summary>
    /// <param name="utf8Json">The file's bytes.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The manifest, or AW-EVIDENCE-026 naming every problem.</returns>
    public static Result<DesktopManifest> Read(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options) => throw new NotSupportedException();
}
