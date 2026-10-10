using Vogen;

// The typed-id defaults every assembly that declares Vogen ids carries (docs/csharp/typed-ids.md, decision R9).
// No Vogen conversions: the System.Text.Json source generator can't see Vogen's converters and would write {}.
// The rest keeps an id's public surface to what ITypedId needs (Value, the bool TryFrom, IParsable): no converter
// factory, no equality with or casts to and from the primitive, no TryFrom returning Vogen's ValueObjectOrError.
// These are example ids for the converter tests; TypedIdGuard (tests/Aw.Architecture.Tests) checks every argument
// in the registered production assemblies, not here (nothing may reference this test project).
[assembly: VogenDefaults(
    conversions: Conversions.None,
    systemTextJsonConverterFactoryGeneration: SystemTextJsonConverterFactoryGeneration.Omit,
    primitiveEqualityGeneration: PrimitiveEqualityGeneration.Omit,
    toPrimitiveCasting: CastOperator.None,
    fromPrimitiveCasting: CastOperator.None,
    tryFromGeneration: TryFromGeneration.GenerateBoolMethod)]
