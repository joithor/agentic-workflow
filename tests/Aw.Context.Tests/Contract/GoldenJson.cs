using System.Text;
using Aw.Kernel.Json;

namespace Aw.Context.Tests.Contract;

/// <summary>A view as AwJson writes it, indented, for a golden file.</summary>
internal static class GoldenJson
{
    private static readonly System.Text.Json.JsonSerializerOptions _options = AwJson.CreateOptions(ContractJsonContext.Default, indented: true);

    internal static string Of<T>(T value) => Encoding.UTF8.GetString(AwJson.Serialize(value, _options)) + "\n";
}
