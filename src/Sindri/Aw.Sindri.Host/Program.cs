using Aw.Sindri.Host;

var app = LoopbackBinding.Build(args, Environment.GetEnvironmentVariable, TimeProvider.System);   // also resolves the telemetry runtime
await using (app.ConfigureAwait(false))
{
    _ = Pipeline.Configure(app);
    await app.RunAsync().ConfigureAwait(false);
}
