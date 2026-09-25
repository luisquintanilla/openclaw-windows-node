using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using OpenClaw.Audio.Testing;
using OpenClawTray.Services;

namespace OpenClaw.Audio.Benchmarks;

internal static class AudioBenchmarkInputs
{
    internal sealed record Entry(string Group, string Profile, int Length, float? Threshold, string Hash, int Fallbacks, int Chunks);
    internal static float[] Signal(int length, string profile)
    {
        // Xorshift32, fixed seed and arithmetic; no runtime-specific Random.
        uint seed = 0xA0D1051A;
        var samples = new float[length];
        for (int i = 0; i < length; i++)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            float noise = ((seed & 0xFFFF) / 32768f - 1f);
            float wave = ((i % 97) - 48) / 48f;
            samples[i] = profile switch
            {
                "Silence" => 0,
                "Low" => noise * 0.0001f,
                "Speech" => wave * 0.09f + noise * 0.03f,
                "Clipped" => Math.Clamp(wave * 3f, -1f, 1f),
                _ => throw new ArgumentException("Unknown signal profile.", nameof(profile))
            };
        }
        return samples;
    }

    internal static float[] VadSignal(string profile, float threshold)
    {
        return Enumerable.Range(0, 512).Select(i => profile switch
        {
            "Quiet" => threshold * 0.2f,
            "Speech" => threshold * (i % 2 == 0 ? 4f : -4f),
            "Mixed" => i % 13 == 0 ? 0.5f : 1e-20f,
            "Below" => MathF.BitDecrement(threshold),
            "At" => threshold,
            "Above" => MathF.BitIncrement(threshold),
            _ => throw new ArgumentException("Unknown VAD profile.", nameof(profile))
        }).ToArray();
    }

    internal static float[][] Stage(string scenario)
    {
        bool speaking = false;
        int silence = 0;
        var result = new float[20][];
        int fallbackChunks = scenario switch
        {
            "Fallback10" => 2, "Fallback50" => 10, "Fallback100" => 20,
            "Fixed" or "Ordinary" or "Clipped" => 0,
            _ => throw new ArgumentException("Unknown stage scenario.", nameof(scenario))
        };
        for (int i = 0; i < result.Length; i++)
        {
            float threshold = speaking ? 0.008f : 0.03f;
            result[i] = i < fallbackChunks
                ? Enumerable.Repeat(threshold / 5f, 512).ToArray()
                : Signal(scenario == "Fixed" ? 1600 : 512,
                    scenario == "Clipped" ? "Clipped" : i % 10 < 6 ? "Speech" : "Silence");
            var postGain = (float[])result[i].Clone();
            AudioNumericsReference.ApplyGain(postGain);
            Advance(AudioNumericsReference.CalculateRms(postGain) >= threshold, ref speaking, ref silence);
        }
        return result;
    }

    internal static void Advance(bool speech, ref bool speaking, ref int silence)
    {
        if (speech)
        {
            speaking = true;
            silence = 0;
        }
        else if (speaking && ++silence >= 3)
            speaking = false;
    }

    internal static List<Entry> VerifyCorpus()
    {
        var entries = new List<Entry>();
        foreach (int length in new[] { 480, 512, 960, 1600, 4096 })
        {
            foreach (string profile in new[] { "Silence", "Low", "Speech", "Clipped" })
            {
                var samples = Signal(length, profile);
                VerifyRms(samples, null);
                VerifyGain(samples);
                entries.Add(new("MeterGain", profile, length, null, Hash(samples), 0, 0));
            }
        }
        foreach (int length in new[] { 16, 31, 32, 33, 511, 513, 4097, 16000 })
        {
            var samples = Signal(length, "Speech");
            VerifyRms(samples, null);
            entries.Add(new("Boundary", "Speech", length, null, Hash(samples), 0, 0));
        }
        foreach (float threshold in new[] { 0.03f, 0.008f })
        {
            foreach (string profile in new[] { "Quiet", "Speech", "Mixed", "Below", "At", "Above" })
            {
                var samples = VadSignal(profile, threshold);
                VerifyRms(samples, threshold);
                int fallback = GuardFallback(samples, threshold) ? 1 : 0;
                entries.Add(new("Vad", profile, 512, threshold, Hash(samples), fallback, 1));
            }
        }
        foreach (string scenario in new[] { "Fixed", "Ordinary", "Clipped", "Fallback10", "Fallback50", "Fallback100" })
        {
            var source = Stage(scenario);
            bool speaking = false;
            int silence = 0, fallbacks = 0;
            foreach (var input in source)
            {
                VerifyGain(input);
                var samples = (float[])input.Clone();
                AudioNumericsReference.ApplyGain(samples);
                VerifyRms(samples, null);
                if (scenario == "Fixed")
                    continue;
                float threshold = speaking ? 0.008f : 0.03f;
                VerifyRms(samples, threshold);
                if (GuardFallback(samples, threshold))
                    fallbacks++;
                Advance(AudioNumericsReference.CalculateRms(samples) >= threshold, ref speaking, ref silence);
            }
            int expected = scenario switch { "Fallback10" => 2, "Fallback50" => 10, "Fallback100" => 20, _ => 0 };
            if (fallbacks != expected)
                throw new InvalidOperationException($"{scenario}: expected {expected} fallbacks, got {fallbacks}.");
            entries.Add(new("Stage", scenario, source[0].Length, null, Hash(source.SelectMany(x => x).ToArray()),
                fallbacks, scenario == "Fixed" ? 0 : 20));
        }
        VerifyGain([-0f, 0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity,
            MathF.BitDecrement(0.2f), 0.2f, MathF.BitIncrement(0.2f),
            MathF.BitDecrement(-0.2f), -0.2f, MathF.BitIncrement(-0.2f)]);
        return entries;
    }

    private static bool GuardFallback(float[] samples, float threshold) =>
        AudioNumerics.IsInsideVadGuard(MathF.Sqrt(TensorPrimitives.SumOfSquares(samples) / samples.Length), threshold);

    private static void VerifyRms(float[] samples, float? threshold)
    {
        float expected = AudioNumericsReference.CalculateRms(samples);
        float actual = threshold is float t ? AudioNumerics.CalculateVadRms(samples, t) : AudioNumerics.CalculateRms(samples);
        if (MathF.Abs(expected - actual) > (threshold.HasValue ? 1f / 16384 : 0.00025f) ||
            threshold is float active && (expected >= active) != (actual >= active))
            throw new InvalidOperationException("Corpus RMS/decision mismatch.");
    }

    private static void VerifyGain(float[] source)
    {
        var expected = (float[])source.Clone();
        var actual = (float[])source.Clone();
        AudioNumericsReference.ApplyGain(expected);
        AudioGainExperiment.Apply(actual);
        for (int i = 0; i < expected.Length; i++)
        {
            if (float.IsNaN(expected[i]) ? !float.IsNaN(actual[i]) :
                BitConverter.SingleToInt32Bits(expected[i]) != BitConverter.SingleToInt32Bits(actual[i]))
                throw new InvalidOperationException($"Gain experiment sample {i} differs.");
        }
    }

    private static string Hash(float[] samples) =>
        Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(samples.AsSpan())));
}
