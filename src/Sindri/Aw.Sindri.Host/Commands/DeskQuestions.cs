using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Host.Commands;

/// <summary>What an interview desk asks, for 'workflow status'.</summary>
internal static class DeskQuestions
{
    internal static JsonArray Questions(JsonElement body, bool fenced) => throw new NotSupportedException();
}
