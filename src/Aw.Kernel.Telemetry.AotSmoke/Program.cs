using Aw.Kernel.Telemetry.AotSmoke;

// Default: run the checks and print one fact per line (run.sh compares the output with expected.txt).
// With "stdin-mode": log each standard-input line at information level and print the JSON lines to stdout; the level
// comes from AW_LOG_LEVEL (an unknown level logs one AW-TELEMETRY-001 warning and stays at information).
return args is ["stdin-mode"] ? await Checks.StdinMode().ConfigureAwait(false) : Checks.Run(Console.Out);
