using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveRerunScriptsStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => RecheckSteps.RerunScripts;

    protected override string ExpectedHash => "sha256:2e0e9a8b887862de2e9374048a01a2c849837c05999ce9db5bcf51a2bd13d732";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
