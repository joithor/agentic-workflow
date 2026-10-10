using System.Text;
using Aw.Kernel;
using Aw.Kernel.AotSmoke;
using Aw.Kernel.Events;
using Aw.Kernel.Json;
using Aw.Kernel.Outbox;

// Exercises Kernel's AOT-sensitive paths in a native binary: source-generated AwJson (write, indent, read back,
// errors), the timestamp converters, Result chaining and the event/outbox seams. run.sh compares the output.
var options = AwJson.CreateOptions(SmokeContext.Default);
var sample = new Sample(
    Name: "caf\u00e9 <b>",
    At: new DateTimeOffset(2026, 10, 10, 8, 0, 0, 7, TimeSpan.FromHours(2)),
    Money: 1.10m,
    Note: null,
    Tags: ["a", "b"],
    Counts: new Dictionary<string, int>(StringComparer.Ordinal) { ["Key"] = 1 },
    Error: KernelErrors.NotFound.With("gone"));
var lines = new List<string>
{
    Encoding.UTF8.GetString(AwJson.Serialize(sample, options)),
    Encoding.UTF8.GetString(AwJson.Serialize(sample, AwJson.CreateOptions(SmokeContext.Default, indented: true))),
};
var back = AwJson.Deserialize<Sample>(AwJson.Serialize(sample, options), options);
lines.Add("roundtrip:" + (back.IsSuccess && back.Value.At == sample.At && back.Value.Tags.SequenceEqual(sample.Tags, StringComparer.Ordinal)));
lines.Add("bad:" + AwJson.Deserialize<Sample>("{\"name\":"u8, options).Error.Code);
lines.Add("offsetless:" + AwJson.Deserialize<Sample>(Encoding.UTF8.GetBytes(
    "{\"name\":\"n\",\"at\":\"2026-10-10T06:00:00\",\"money\":1,\"note\":null,\"tags\":[],\"counts\":{},"
    + "\"error\":{\"code\":\"AW-X-001\",\"kind\":\"invalid\",\"message\":\"m\"}}"), options).Error.Code);
var chained = await ValueTask.FromResult<Result<int>>(20).Map(v => v + 1).BindAsync(v => ValueTask.FromResult(
    v % 2 == 0 ? Results.Success(v) : Results.Failure<int>(KernelErrors.Invalid.With("odd")))).ConfigureAwait(false);
lines.Add("chain:" + chained.Match(v => "value", e => e.Code));
var clock = new SmokeClock(sample.At);
var ids = new SmokeIds();
var context = EventContext.Create("host:smoke", "personal", 0, ids.NewId()).Value;
var envelope = context.Envelope("smoke.ran", sample, clock, ids);
lines.Add(Encoding.UTF8.GetString(AwJson.Serialize(context.CausedBy(envelope).Command("smoke.notify", "{}", clock, ids), options)));
lines.Add("retry:" + OutboxRetryPolicy.Default.NextAttempt(3, sample.At)?.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
Console.Out.Write(string.Join('\n', lines) + "\n");
