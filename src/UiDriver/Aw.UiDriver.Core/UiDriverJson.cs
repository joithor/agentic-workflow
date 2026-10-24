using System.Text.Json;
using Aw.Kernel.Json;

namespace Aw.UiDriver.Core;

/// <summary>The package's JSON options: the Kernel profile over <see cref="UiDriverJsonContext"/>, shared by the server and the client.</summary>
internal static class UiDriverJson
{
    /// <summary>The options.</summary>
    internal static JsonSerializerOptions Options { get; } = AwJson.CreateOptions(UiDriverJsonContext.Default);
}
