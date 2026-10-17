using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class VerifyJourneyStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ProofSteps.VerifyJourney;

    protected override string ExpectedHash => "sha256:f8b1dcef144b670930540e962818c004b78f02d84803253e5d96f18ff4c3e877";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
