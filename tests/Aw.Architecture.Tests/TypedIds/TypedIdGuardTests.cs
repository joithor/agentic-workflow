using Aw.Architecture.Tests.TypedIds.GuardFixtures;
using Aw.Kernel;

namespace Aw.Architecture.Tests.TypedIds;

[Trait("Lane", "integration")]
public sealed class TypedIdGuardTests
{
    [Fact]
    public void This_assembly_sets_every_vogen_default_the_recipe_requires() =>
        Assert.Empty(TypedIdGuard.CheckDefaults(typeof(ItemId).Assembly));

    [Fact]
    public void An_assembly_without_vogen_defaults_is_reported_by_name() =>
        Assert.Contains("has no [assembly: VogenDefaults", Assert.Single(TypedIdGuard.CheckDefaults(typeof(Result<>).Assembly)), StringComparison.Ordinal);

    [Fact]
    public void Every_id_and_record_in_this_assembly_passes_the_guard()
    {
        var types = typeof(ItemId).Assembly.GetTypes().Where(t => t.Namespace == typeof(ItemId).Namespace).ToList();

        Assert.Contains(typeof(TypedIdPayload), types);
        Assert.Empty(TypedIdGuard.CheckTypes(types));
    }

    [Fact]
    public void A_value_object_without_a_json_converter_is_reported() =>
        Assert.Contains("would serialize as {}", Assert.Single(TypedIdGuard.CheckTypes([typeof(UnconvertedId)])), StringComparison.Ordinal);

    [Fact]
    public void The_non_generic_value_object_attribute_is_matched_too()
    {
        var violations = TypedIdGuard.CheckTypes([typeof(NonGenericId)]);

        Assert.Equal(2, violations.Count);
        Assert.Contains(violations, v => v.Contains("[JsonConverter]", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.Contains("ITypedId", StringComparison.Ordinal));
    }

    [Fact]
    public void A_value_object_with_its_own_converter_but_no_typed_id_contract_is_reported() =>
        Assert.Contains("ITypedId", Assert.Single(TypedIdGuard.CheckTypes([typeof(HandConvertedId)])), StringComparison.Ordinal);

    [Fact]
    public void A_record_with_an_id_the_json_profile_would_not_require_is_reported() =>
        Assert.Contains("OptionalIdRecord.Item", Assert.Single(TypedIdGuard.CheckTypes([typeof(OptionalIdRecord)])), StringComparison.Ordinal);

    [Fact]
    public void A_constructor_parameter_counts_only_on_the_constructor_the_serializer_uses() =>
        Assert.Contains("ParameterlessCtorRecord.Item", Assert.Single(TypedIdGuard.CheckTypes([typeof(ParameterlessCtorRecord)])), StringComparison.Ordinal);

    [Fact]
    public void Nullable_ids_and_required_members_pass() =>
        Assert.Empty(TypedIdGuard.CheckTypes([typeof(RequiredMemberRecord)]));
}
