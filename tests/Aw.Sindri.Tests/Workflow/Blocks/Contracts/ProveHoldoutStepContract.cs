using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The holdout Step of the line's prove station, descriptor-only (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveHoldoutStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ProveSteps.Holdout;

    protected override string ExpectedHash => "sha256:03afc64c804a3f055d9933c0edd4168b2ae6e224494ce23bda4b5b35422c2493";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
