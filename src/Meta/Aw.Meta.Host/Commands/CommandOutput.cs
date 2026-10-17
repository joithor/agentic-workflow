using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Meta.Host.Commands;

/// <summary>
/// Maps a command's Result to output and an exit code: 0 ok, 1 attention (something is open or refused),
/// 2 a usage or tool error. Errors are one stderr line "AW-META-NNN: reason".
/// </summary>
internal static class CommandOutput
{
    internal const int Ok = 0;
    internal const int Attention = 1;
    internal const int Failed = 2;

    /// <summary>Calls the command's service and prints the response as JSON (--json) or as <paramref name="text"/>.</summary>
    /// <param name="context">The command's context.</param>
    /// <param name="request">The request.</param>
    /// <param name="text">The text form of the response.</param>
    /// <param name="attention">Whether the response needs attention (exit 1); null for never.</param>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <returns>The exit code.</returns>
    internal static async Task<int> Call<TRequest, TResponse>(
        CommandContext context,
        TRequest request,
        Func<TResponse, string> text,
        Func<TResponse, bool>? attention = null)
        where TRequest : notnull
    {
        var service = context.Services.GetRequiredService<IService<TRequest, TResponse>>();
        var result = await service.Handle(request, context.Cancellation).ConfigureAwait(false);
        return Report(context, result, text, attention);
    }

    internal static int Report<T>(CommandContext context, Result<T> result, Func<T, string> text, Func<T, bool>? attention = null)
    {
        if (result.IsError)
        {
            return Fail(context, result.Error);
        }

        var body = context.Line.Json ? Encoding.UTF8.GetString(AwJson.Serialize(result.Value, context.Json)) : text(result.Value);
        if (body.Length > 0)
        {
            context.Io.Out.WriteLine(body);
        }

        return attention?.Invoke(result.Value) == true ? Attention : Ok;
    }

    internal static int Fail(CommandContext context, Error error)
    {
        context.Io.Err.WriteLine($"{error.Code}: {error.Message.ReplaceLineEndings(" ")}");
        return Failed;
    }

    internal static int Usage(ConsoleIo io, string problem, string usage)
    {
        io.Err.WriteLine($"{MetaErrors.Invalid.Code}: {problem}; usage: aw-meta {usage}");
        return Failed;
    }
}
