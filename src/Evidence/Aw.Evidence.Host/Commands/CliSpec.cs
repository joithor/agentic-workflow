namespace Aw.Evidence.Host.Commands;

/// <summary>A command's argument shape: how many positionals, which flags take a value, which are switches.</summary>
/// <param name="Positionals">The exact number of positional arguments.</param>
/// <param name="ValueFlags">Flags followed by one value.</param>
/// <param name="Switches">Flags that stand alone.</param>
internal sealed record CliSpec(int Positionals, IReadOnlyCollection<string> ValueFlags, IReadOnlyCollection<string> Switches);
