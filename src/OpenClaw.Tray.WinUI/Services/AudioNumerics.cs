using System;
using System.Numerics.Tensors;
using System.Runtime.Intrinsics;

namespace OpenClawTray.Services;

// RMS inputs are post-gain/clamp samples in [-1, 1], or NaN. The bounded SIMD
// domain is derived in benchmarks/OpenClaw.Audio.Benchmarks/README.md.
internal static class AudioNumerics
{
    internal static void ApplyGain(Span<float> samples)
    {
        TensorPrimitives.Multiply(samples, 5f, samples);
        TensorPrimitives.Clamp(samples, -1f, 1f, samples);
    }

    internal static float CalculateRms(ReadOnlySpan<float> samples)
    {
        if (samples.Length < 32 || samples.Length > 4096 || !IsAccelerated)
            return CalculateScalarRms(samples);

        float rms = MathF.Sqrt(TensorPrimitives.SumOfSquares(samples) / samples.Length);
        return float.IsFinite(rms) ? rms : CalculateScalarRms(samples);
    }

    private static bool IsAccelerated =>
        Vector128.IsHardwareAccelerated || Vector256.IsHardwareAccelerated || Vector512.IsHardwareAccelerated;

    private static float CalculateScalarRms(ReadOnlySpan<float> samples)
    {
        float sum = 0;
        for (int i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];
        return MathF.Sqrt(sum / samples.Length);
    }
}
