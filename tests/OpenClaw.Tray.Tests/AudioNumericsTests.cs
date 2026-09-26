using System.Numerics.Tensors;
using System.Runtime.Intrinsics;
using OpenClaw.Audio.Testing;
using OpenClaw.Audio.Benchmarks;
using OpenClawTray.Services;

namespace OpenClaw.Tray.Tests;

public sealed class AudioNumericsTests
{
    public static TheoryData<int> Shapes => new()
    {
        0, 1, 2, 3, 4, 7, 8, 15, 16, 17, 31, 32, 33, 160, 480, 511, 512, 513,
        960, 1024, 1600, 4095, 4096, 4097, 16000
    };

    [Theory]
    [MemberData(nameof(Shapes))]
    public void ShapesSignalsAndOffsetSlices_PreserveBoundsFallbackAndInput(int length)
    {
        foreach (var samples in Signals(length))
        {
            var padded = new float[length + 7];
            samples.CopyTo(padded, 3);
            var before = padded.Select(BitConverter.SingleToInt32Bits).ToArray();
            ReadOnlySpan<float> input = padded.AsSpan(3, length);
            float expected = AudioNumericsReference.CalculateRms(input);
            float actual = AudioNumerics.CalculateRms(input);
            AssertCompatible(expected, actual, 0.00025f);
            AssertCompatible(Math.Clamp(expected * 3f, 0, 1), Math.Clamp(actual * 3f, 0, 1), 1f / 1024);
            if (length < 32 || length > 4096 ||
                !(Vector128.IsHardwareAccelerated || Vector256.IsHardwareAccelerated || Vector512.IsHardwareAccelerated))
                AssertSameBitsOrNaN(expected, actual);
            Assert.Equal(before, padded.Select(BitConverter.SingleToInt32Bits));
        }
    }

    [Theory]
    [InlineData(0.03f)]
    [InlineData(0.008f)]
    public void ThresholdNeighborhood_8193AdjacentAmplitudes_PreserveExactDecisions(float threshold)
    {
        int center = BitConverter.SingleToInt32Bits(threshold);
        var samples = new float[512];
        for (int offset = -4096; offset <= 4096; offset++)
        {
            Array.Fill(samples, BitConverter.Int32BitsToSingle(center + offset));
            AssertVad(samples, threshold);
        }
    }

    [Theory]
    [InlineData(0.03f)]
    [InlineData(0.008f)]
    public void AdversarialReductions_PreserveDecisionAndInclusiveFallback(float threshold)
    {
        var random = new Random(0x51A7);
        var patterns = Signals(512).ToList();
        for (int corpus = 0; corpus < 256; corpus++)
            patterns.Add(Enumerable.Range(0, 512).Select(_ => (float)(random.NextDouble() * 2 - 1)).ToArray());
        foreach (float[] pattern in patterns)
        {
            float original = AudioNumericsReference.CalculateRms(pattern);
            if (original <= 0 || !float.IsFinite(original))
                continue;
            foreach (float scale in new[] { 0.998f, 0.999f, MathF.BitDecrement(1f), 1f, MathF.BitIncrement(1f), 1.001f, 1.002f })
            {
                var samples = pattern.Select(value => Math.Clamp(value * (threshold / original) * scale, -1f, 1f)).ToArray();
                AssertVad(samples, threshold);
                Array.Sort(samples, (left, right) => MathF.Abs(left).CompareTo(MathF.Abs(right)));
                AssertVad(samples, threshold);
                Array.Reverse(samples);
                AssertVad(samples, threshold);
            }
        }
    }

    [Theory]
    [InlineData(0.03f)]
    [InlineData(0.008f)]
    public void GuardEndpoints_AreClosedExactDoubleIntervals(float threshold)
    {
        double lower = (double)threshold * (1 - 1.0 / 1024);
        double upper = (double)threshold * (1 + 1.0 / 1024);
        Assert.False(AudioVadExperiment.IsInsideVadGuard(Math.BitDecrement(lower), threshold));
        Assert.True(AudioVadExperiment.IsInsideVadGuard(lower, threshold));
        Assert.True(AudioVadExperiment.IsInsideVadGuard(Math.BitIncrement(lower), threshold));
        Assert.True(AudioVadExperiment.IsInsideVadGuard(Math.BitDecrement(upper), threshold));
        Assert.True(AudioVadExperiment.IsInsideVadGuard(upper, threshold));
        Assert.False(AudioVadExperiment.IsInsideVadGuard(Math.BitIncrement(upper), threshold));
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Gain_InPlaceSlices_PreserveEveryBitAndExceptionalClassification(int length)
    {
        float[] values = [-0f, 0f, float.Epsilon, -float.Epsilon, float.NaN,
            float.PositiveInfinity, float.NegativeInfinity, float.MaxValue, -float.MaxValue,
            MathF.BitDecrement(0.2f), 0.2f, MathF.BitIncrement(0.2f),
            MathF.BitDecrement(-0.2f), -0.2f, MathF.BitIncrement(-0.2f)];
        foreach (float[] signal in Signals(length).Append(
            Enumerable.Range(0, length).Select(i => values[i % values.Length]).ToArray()))
        {
            var expected = new float[length + 7];
            Array.Fill(expected, 0.0625f);
            signal.CopyTo(expected, 3);
            var actual = (float[])expected.Clone();
            AudioNumericsReference.ApplyGain(expected.AsSpan(3, length));
            AudioNumerics.ApplyGain(actual.AsSpan(3, length));
            for (int i = 0; i < expected.Length; i++)
                AssertSameBitsOrNaN(expected[i], actual[i]);
        }
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void UnsupportedVadShapesAndThresholds_UseOriginalOrder(int length)
    {
        float[] samples = Signals(length).Last();
        float expected = AudioNumericsReference.CalculateRms(samples);
        foreach (float threshold in new[] { 0f, -0.03f, 0.1f, float.NaN, float.PositiveInfinity })
            AssertSameBitsOrNaN(expected, AudioVadExperiment.CalculateVadRms(samples, threshold));
        if (length != 512)
        {
            AssertSameBitsOrNaN(expected, AudioVadExperiment.CalculateVadRms(samples, 0.03f));
            AssertSameBitsOrNaN(expected, AudioVadExperiment.CalculateVadRms(samples, 0.008f));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    [InlineData(511)]
    public void NaNAndPostGainInfinities_PreserveClassification(int position)
    {
        var samples = Enumerable.Repeat(0.1f, 512).ToArray();
        samples[position] = float.NaN;
        Assert.True(float.IsNaN(AudioNumerics.CalculateRms(samples)));
        Assert.True(float.IsNaN(AudioVadExperiment.CalculateVadRms(samples, 0.03f)));
        foreach (float infinity in new[] { float.NegativeInfinity, float.PositiveInfinity })
        {
            samples[position] = infinity;
            AudioNumericsReference.ApplyGain(samples);
            Assert.Equal(MathF.CopySign(1, infinity), samples[position]);
            AssertVad(samples, 0.03f);
        }
    }

    [Fact]
    public void EmptyRms_RemainsNaN_NotSilentZero()
    {
        Assert.True(float.IsNaN(AudioNumerics.CalculateRms([])));
        Assert.True(float.IsNaN(AudioVadExperiment.CalculateVadRms([], 0.03f)));
    }

    private static void AssertVad(float[] samples, float threshold)
    {
        var before = samples.Select(BitConverter.SingleToInt32Bits).ToArray();
        float expected = AudioNumericsReference.CalculateRms(samples);
        float actual = AudioVadExperiment.CalculateVadRms(samples, threshold);
        Assert.Equal(expected >= threshold, actual >= threshold);
        AssertCompatible(expected, actual, 1f / 16384);
        float vector = MathF.Sqrt(TensorPrimitives.SumOfSquares(samples) / samples.Length);
        if (!float.IsFinite(vector) || AudioVadExperiment.IsInsideVadGuard(vector, threshold))
            AssertSameBitsOrNaN(expected, actual);
        Assert.Equal(before, samples.Select(BitConverter.SingleToInt32Bits));
    }

    private static void AssertSameBitsOrNaN(float expected, float actual)
    {
        if (float.IsNaN(expected))
            Assert.True(float.IsNaN(actual));
        else
            Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual));
    }

    private static void AssertCompatible(float expected, float actual, float tolerance)
    {
        if (!float.IsFinite(expected))
            AssertSameBitsOrNaN(expected, actual);
        else
        {
            Assert.True(float.IsFinite(actual));
            Assert.InRange(MathF.Abs(expected - actual), 0f, tolerance);
        }
    }

    private static IEnumerable<float[]> Signals(int length)
    {
        yield return new float[length];
        yield return Enumerable.Repeat(-0f, length).ToArray();
        foreach (float value in new[] { 0.125f, -0.125f, 1f, -1f, float.Epsilon, 1.17549435E-38f })
            yield return Enumerable.Repeat(value, length).ToArray();
        if (length > 0)
        {
            foreach (int position in new[] { 0, length / 2, length - 1 })
            {
                var impulse = new float[length];
                impulse[position] = 1f;
                yield return impulse;
            }
        }
        yield return Enumerable.Range(0, length).Select(i => i % 2 == 0 ? 1f : -1f).ToArray();
        yield return Enumerable.Range(0, length).Select(i => i % 17 == 0 ? 1f : 1e-20f).ToArray();
        yield return Enumerable.Range(0, length).Select(i => Math.Clamp((float)(2 * Math.Sin(i * 0.17)), -1f, 1f)).ToArray();
        var random = new Random(0xA0D10);
        yield return Enumerable.Range(0, length).Select(_ => (float)(random.NextDouble() * 2e-5 - 1e-5)).ToArray();
        yield return Enumerable.Range(0, length).Select(i => (float)(0.09 * Math.Sin(i * 0.073) + 0.04 * Math.Cos(i * 0.037))).ToArray();
    }
}
