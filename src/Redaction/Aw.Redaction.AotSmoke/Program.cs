using System.Globalization;
using Aw.Redaction;
using Aw.Redaction.AotSmoke;

// Default: scrub the built-in samples and print one line each (run.sh compares the output with expected.txt).
// With "--stdin": scrub standard input, write the text to stdout and the outcome and hits to stderr.
if (args is ["--stdin"])
{
    var result = Redactor.Default.Scrub(await Console.In.ReadToEndAsync().ConfigureAwait(false));
    await Console.Out.WriteAsync(result.Text).ConfigureAwait(false);
    await Console.Error.WriteLineAsync(Describe(result)).ConfigureAwait(false);
    return result.IsBlocked ? 3 : 0;
}

foreach (var (name, input) in Samples.All)
{
    var result = Redactor.Default.Scrub(input);
    var text = result.Text.Replace("\n", "\\n", StringComparison.Ordinal);
    Console.Out.Write($"{name}: {text} | {Describe(result)}\n");
}

return 0;

static string Describe(ScrubResult result) =>
    string.Join(' ', result.Hits.Select(h => string.Create(CultureInfo.InvariantCulture, $"{h.Rule}@{h.Start}-{h.End}"))
        .Prepend(result.Outcome.ToString().ToLowerInvariant()));
