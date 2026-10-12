using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Ids;

namespace Aw.Sindri.Core.Commands;

/// <summary>
/// A catalog row's CLI form (UI spec E-16): 'sindri' and checked parts only, so a copied command can't carry a newline
/// or a second command. Parts are lower-case words, a fixed '--flag', a '&lt;placeholder&gt;' or an '[optional part]'.
/// </summary>
public sealed partial record CliForm
{
    private CliForm(string text)
    {
        Text = text;
    }

    /// <summary>The form, for example 'sindri profile approve &lt;hash&gt;'.</summary>
    public string Text { get; }

    /// <summary>Checks <paramref name="text"/> against the CLI form's shape.</summary>
    /// <param name="text">The form.</param>
    /// <returns>The form, or SND-CLI-003 when it has a part outside the shape.</returns>
    public static Result<CliForm> Parse(string text) =>
        Shape.IsMatch(text)
            ? new CliForm(text)
            : SindriErrors.InvalidInput.With($"'{text}' is not a CLI form: 'sindri' then words, --flags, <placeholders> or [optional parts]");

    /// <summary>The form with the typed id <paramref name="id"/> in place of '&lt;<paramref name="placeholder"/>&gt;' (P4's NeedsYouRow.cli).</summary>
    /// <param name="placeholder">The placeholder's name, for example 'itemId'.</param>
    /// <param name="id">The id to substitute.</param>
    /// <typeparam name="TId">A typed id over text.</typeparam>
    /// <returns>The filled form, or SND-CLI-003 when the form has no such placeholder or the id's text isn't safe on a command line.</returns>
    public Result<CliForm> Fill<TId>(string placeholder, TId id)
        where TId : struct, ITypedId<TId, string>
        => throw new NotSupportedException($"CliForm.Fill: make CliFormTests pass ({placeholder}, {id.Value})");

    /// <inheritdoc />
    public override string ToString() => Text;

    [GeneratedRegex("^sindri( [a-z][a-z-]*| --[a-z][a-z-]*| <[a-zA-Z]+>| \\[[^\\]\\r\\n]+\\])*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
