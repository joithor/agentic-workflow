using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'session.ended' payload (API spec §8.1): the session's end, with its final token usage when the target's reader could tell. Key 'session.ended:&lt;launch&gt;'.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Reason">Exit or lost.</param>
/// <param name="ExitCode">The agent's exit code for <see cref="SessionEndReason.Exit"/>, otherwise null.</param>
/// <param name="InputTokens">Input tokens in all, or null when the reader had none.</param>
/// <param name="OutputTokens">Output tokens in all, or null when the reader had none.</param>
public sealed record SessionEnded([property: JsonPropertyName("launchId")] LaunchId Launch, SessionEndReason Reason, int? ExitCode, long? InputTokens = null, long? OutputTokens = null);
