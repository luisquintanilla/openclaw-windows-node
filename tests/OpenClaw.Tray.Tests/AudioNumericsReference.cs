namespace OpenClaw.Audio.Testing;

// Frozen from AudioPipeline.cs at 273b0182745a3093c0e09f306ca8a1fff6ef3c5a.
// Keep independent of production helpers, including accumulation order.
internal static class AudioNumericsReference
{
    internal static float CalculateRms(ReadOnlySpan<float> samples)
    {
        float sum = 0;
        for (int i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];
        return MathF.Sqrt(sum / samples.Length);
    }

    internal static void ApplyGain(Span<float> samples)
    {
        const float gain = 5.0f;
        for (int i = 0; i < samples.Length; i++)
            samples[i] = Math.Clamp(samples[i] * gain, -1.0f, 1.0f);
    }
}
