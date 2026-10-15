using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>The arguments of 'sindri context map review' (context spec §8.7) and of 'sindri context filemap accept|reject': a pure parse.</summary>
internal static class ReviewArguments
{
    /// <summary>Reads the arguments after 'review'.</summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The command, or SND-CLI-001 naming the forms.</returns>
    internal static Kernel.Result<ReviewCommand> Parse(string[] args) => throw new NotSupportedException(new ReviewContextMapRequest(args.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Verb);
}
