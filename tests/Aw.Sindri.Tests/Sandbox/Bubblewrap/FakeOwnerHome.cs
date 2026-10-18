using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Infrastructure.Sandbox;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The fixture homes the owner-copy source reads: a fake owner home with a Claude login in the state the test names.</summary>
internal sealed class FakeOwnerHome : IDisposable
{
    internal static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    internal FakeOwnerHome(LoginState state, TimeSpan? lifetime = null)
    {
        Root = Path.Combine(Path.GetTempPath(), "aw-owner-copy-" + RealTime.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Root + "/.claude/hooks");
        Directory.CreateDirectory(Root + "/.claude/projects/-x");
        Directory.CreateDirectory(Root + "/.codex/sessions");
        File.WriteAllText(Root + "/.claude/settings.json", "{}");
        File.WriteAllText(Root + "/.claude/statusline.sh", "#!/bin/sh");
        File.WriteAllText(Root + "/.claude.json", "{\"onboarded\":true}");
        File.WriteAllText(Root + "/.codex/config.toml", "model = \"x\"");
        if (state != LoginState.Missing)
        {
            var expires = Now + (state == LoginState.Expired ? TimeSpan.FromMinutes(5) : lifetime ?? TimeSpan.FromHours(8));
            File.WriteAllText(Root + "/.claude/.credentials.json", JsonSerializer.Serialize(new { claudeAiOauth = new { accessToken = "a", refreshToken = "r", expiresAt = expires.ToUnixTimeMilliseconds() } }));
            File.WriteAllText(Root + "/.codex/auth.json", "{\"last_refresh\":\"2026-10-01\"}");
        }
    }

    internal string Root { get; }

    internal string SindriHome => Root + "/sindri";

    internal OwnerCopyCredentialSource Source() =>
        new(new OwnerCopyOptions(Root, SindriHome, OwnerCopyOptions.DefaultMinRemaining), new FakeTimeProvider(Now));

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
