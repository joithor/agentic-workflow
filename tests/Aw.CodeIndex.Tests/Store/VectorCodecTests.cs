using Aw.CodeIndex.Infrastructure.Store;
using Aw.Kernel.Testing;

namespace Aw.CodeIndex.Tests.Store;

[Lane(TestLane.Unit)]
public sealed class VectorCodecTests
{
    [Fact]
    public void A_vector_round_trips_as_little_endian_floats()
    {
        float[] vector = [1.5f, -2.25f, 0f, float.MaxValue];

        var bytes = VectorCodec.Encode(vector);

        Assert.Equal(16, bytes.Length);
        Assert.Equal([0x00, 0x00, 0xC0, 0x3F], bytes[..4]);
        Assert.Equal(vector, VectorCodec.Decode(bytes));
    }

    [Fact]
    public void A_blob_that_is_not_a_whole_number_of_floats_is_damage()
        => _ = Assert.Throws<InvalidDataException>(() => VectorCodec.Decode(new byte[6]));

    [Fact]
    public void Cosine_is_one_for_the_same_direction_zero_for_orthogonal_and_zero_for_a_zero_vector()
    {
        Assert.Equal(1.0, VectorCodec.Cosine([1, 2, 3], [2, 4, 6]), precision: 9);
        Assert.Equal(0.0, VectorCodec.Cosine([1, 0], [0, 1]), precision: 9);
        Assert.Equal(0.0, VectorCodec.Cosine([0, 0], [1, 1]), precision: 9);
    }

    [Fact]
    public void Cosine_compares_the_shared_length_when_the_vectors_differ_in_size()
        => Assert.Equal(1.0, VectorCodec.Cosine([1, 0, 9], [3, 0]), precision: 9);
}
