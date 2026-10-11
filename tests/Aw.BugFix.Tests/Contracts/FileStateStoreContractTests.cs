using Aw.BugFix.Core.Ports;
using Aw.BugFix.Host;
using Aw.BugFix.Infrastructure;
using Aw.BugFix.Testing;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Contracts;

[Lane(TestLane.Integration)]
public sealed class FileStateStoreContractTests : StateStoreContract, IDisposable
{
    private readonly TempDirectory _temp = new();

    protected override IStateStore Store { get; } = new FileStateStore(AwJson.CreateOptions(BugFixJsonContext.Default), new BugFixSettings(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(200)));

    protected override string Directory => Path.Combine(_temp.Path, "state");

    public void Dispose() => _temp.Dispose();

    protected override void Corrupt() => TempDirectory.Write(Directory, "state.json", "{\"schemaVersion\":");
}
