using System.Numerics.Tensors;

namespace OpenClaw.Audio.Benchmarks;

// Experiment only. Shipping gain remains the original fused scalar loop.
internal static class AudioGainExperiment
{
    internal static void Apply(Span<float> samples)
    {
        TensorPrimitives.Multiply(samples, 5f, samples);
        TensorPrimitives.Clamp(samples, -1f, 1f, samples);
    }
}
