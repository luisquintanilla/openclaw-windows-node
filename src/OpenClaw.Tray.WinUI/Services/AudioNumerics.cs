using System;
using System.Numerics.Tensors;
using System.Runtime.Intrinsics;

namespace OpenClawTray.Services;

// Inputs are post-gain/clamp samples in [-1, 1], or NaN. The bounded SIMD
// domain and VAD guard are derived in benchmarks/OpenClaw.Audio.Benchmarks/README.md.
internal static class AudioNumerics
{
    internal static float CalculateRms(ReadOnlySpan<float> samples)
    {
        if (samples.Length < 32 || samples.Length > 4096 || !IsAccelerated)
            return CalculateScalarRms(samples);

        float rms = MathF.Sqrt(TensorPrimitives.SumOfSquares(samples) / samples.Length);
        return float.IsFinite(rms) ? rms : CalculateScalarRms(samples);
    }

    internal static float CalculateVadRms(ReadOnlySpan<float> samples, float activeThreshold)
    {
        if (samples.Length != 512 || (activeThreshold != 0.03f && activeThreshold != 0.008f) || !IsAccelerated)
            return CalculateScalarRms(samples);

        float rms = MathF.Sqrt(TensorPrimitives.SumOfSquares(samples) / samples.Length);
        return !float.IsFinite(rms) || IsInsideVadGuard(rms, activeThreshold)
            ? CalculateScalarRms(samples)
            : rms;
    }

    internal static bool IsInsideVadGuard(double rms, float threshold)
    {
        const double delta = 1.0 / 1024;
        // Exact binary64 products of the promoted binary32 threshold. Closed
        // endpoints must not be rounded inward to float.
        return rms >= (double)threshold * (1 - delta)
            && rms <= (double)threshold * (1 + delta);
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
