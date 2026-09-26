using BenchmarkDotNet.Attributes;
using OpenClaw.Audio.Testing;
using OpenClawTray.Services;

namespace OpenClaw.Audio.Benchmarks;

public class AudioRmsBenchmarks
{
    [Params(480, 512, 960, 1600, 4096)]
    public int Length { get; set; }
    [Params("Silence", "Speech", "Clipped")]
    public string Profile { get; set; } = "";
    private float[] _samples = [];
    [GlobalSetup]
    public void Setup() => _samples = AudioBenchmarkInputs.Signal(Length, Profile);
    [Benchmark(Baseline = true)]
    public float Scalar() => AudioNumericsReference.CalculateRms(_samples);
    [Benchmark]
    public float Meter() => AudioNumerics.CalculateRms(_samples);
}

public class AudioRmsBoundaryBenchmarks
{
    [Params(16, 31, 32, 33, 511, 513, 4097, 16000)]
    public int Length { get; set; }
    private float[] _samples = [];
    [GlobalSetup]
    public void Setup() => _samples = AudioBenchmarkInputs.Signal(Length, "Speech");
    [Benchmark(Baseline = true)]
    public float Scalar() => AudioNumericsReference.CalculateRms(_samples);
    [Benchmark]
    public float Meter() => AudioNumerics.CalculateRms(_samples);
}

public class AudioVadBenchmarks
{
    [Params(0.03f, 0.008f)]
    public float Threshold { get; set; }
    [Params("Quiet", "Speech", "Mixed", "Below", "At", "Above")]
    public string Profile { get; set; } = "";
    private float[] _samples = [];
    [GlobalSetup]
    public void Setup() => _samples = AudioBenchmarkInputs.VadSignal(Profile, Threshold);
    [Benchmark(Baseline = true)]
    public (float, bool) Scalar()
    {
        float rms = AudioNumericsReference.CalculateRms(_samples);
        return (rms, rms >= Threshold);
    }
    [Benchmark]
    public (float, bool) Guarded()
    {
        float rms = AudioVadExperiment.CalculateVadRms(_samples, Threshold);
        return (rms, rms >= Threshold);
    }
}
