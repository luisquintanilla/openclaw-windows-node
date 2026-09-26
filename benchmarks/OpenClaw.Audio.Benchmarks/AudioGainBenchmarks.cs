using BenchmarkDotNet.Attributes;
using OpenClaw.Audio.Testing;
using OpenClawTray.Services;

namespace OpenClaw.Audio.Benchmarks;

public class AudioGainBenchmarks
{
    [Params(480, 512, 960, 1600, 4096)]
    public int Length { get; set; }
    [Params("Low", "Speech", "Clipped")]
    public string Profile { get; set; } = "";
    private float[] _source = [], _work = [];
    [GlobalSetup]
    public void Setup()
    {
        _source = AudioBenchmarkInputs.Signal(Length, Profile);
        _work = new float[Length];
    }
    [Benchmark(Baseline = true, Description = "copy + scalar gain")]
    public float[] Scalar()
    {
        _source.CopyTo(_work, 0);
        AudioNumericsReference.ApplyGain(_work);
        return _work;
    }
    [Benchmark(Description = "copy + tensor gain")]
    public float[] Tensor()
    {
        _source.CopyTo(_work, 0);
        AudioNumerics.ApplyGain(_work);
        return _work;
    }
    [Benchmark(Description = "copy only (diagnostic)")]
    public float[] CopyOnly()
    {
        _source.CopyTo(_work, 0);
        return _work;
    }
}
