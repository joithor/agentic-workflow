using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Forge.Host.Commands;

/// <summary>Maps a command's Result to output and an exit code: 0 success, 1 an expected error, 2 a usage error.</summary>
internal static class CommandOutput
{
    internal static int Write<T>(Result<T> result, JsonSerializerOptions json)
    {
        if (result.IsError)
        {
            Console.Error.WriteLine(Encoding.UTF8.GetString(AwJson.Serialize(result.Error, json)));
            return 1;
        }

        Console.Out.WriteLine(Encoding.UTF8.GetString(AwJson.Serialize(result.Value, json)));
        return 0;
    }

    internal static int Usage(string message)
    {
        Console.Error.WriteLine($"usage: {message}");
        return 2;
    }
}
