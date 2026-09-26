using BenchmarkDotNet.Attributes;
using OpenClaw.Audio.Testing;
using OpenClawTray.Services;

namespace OpenClaw.Audio.Benchmarks;

public class AudioStageBenchmarks
{
    [Params("Fixed", "Ordinary", "Clipped", "Fallback10", "Fallback50", "Fallback100")]
    public string Scenario { get; set; } = "";
    private float[][] _source = [], _work = [];
    [GlobalSetup]
    public void Setup()
    {
        _source = AudioBenchmarkInputs.Stage(Scenario);
        _work = _source.Select(samples => new float[samples.Length]).ToArray();
    }

    [Benchmark(Baseline = true, Description = "copy + scalar stage")]
    public float Scalar() => Run(meter: false, vad: false, gain: false);
    [Benchmark(Description = "copy + meter stage")]
    public float Meter() => Run(meter: true, vad: false, gain: false);
    [Benchmark(Description = "copy + meter + VAD stage")]
    public float MeterVad() => Run(meter: true, vad: true, gain: false);
    [Benchmark(Description = "copy + meter + gain stage")]
    public float MeterGain() => Run(meter: true, vad: false, gain: true);
    [Benchmark(Description = "copy + all stage")]
    public float All() => Run(meter: true, vad: true, gain: true);

    private float Run(bool meter, bool vad, bool gain)
    {
        float checksum = 0;
        bool speaking = false;
        int silence = 0;
        for (int i = 0; i < _source.Length; i++)
        {
            float[] work = _work[i];
            _source[i].CopyTo(work, 0);
            if (gain)
                AudioNumerics.ApplyGain(work);
            else
                AudioNumericsReference.ApplyGain(work);
            float rms = meter ? AudioNumerics.CalculateRms(work) : AudioNumericsReference.CalculateRms(work);
            checksum += Math.Clamp(rms * 3f, 0f, 1f);
            if (Scenario == "Fixed")
                continue;
            float threshold = speaking ? 0.008f : 0.03f;
            float energy = vad ? AudioVadExperiment.CalculateVadRms(work, threshold) : AudioNumericsReference.CalculateRms(work);
            bool speech = energy >= threshold;
            checksum += energy + (speech ? 1f : 0f);
            AudioBenchmarkInputs.Advance(speech, ref speaking, ref silence);
        }
        return checksum;
    }
}
