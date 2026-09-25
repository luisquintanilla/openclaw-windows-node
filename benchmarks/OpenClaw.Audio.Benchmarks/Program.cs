using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text.Json;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using OpenClaw.Audio.Benchmarks;

bool smoke = args.Contains("--smoke");
bool verify = args.Contains("--verify");
args = args.Where(arg => arg is not "--smoke" and not "--verify").ToArray();
string Git(string arguments)
{
    using var process = Process.Start(new ProcessStartInfo("git", arguments)
    {
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true
    }) ?? throw new InvalidOperationException("Could not start git.");
    string output = process.StandardOutput.ReadToEnd().Trim();
    process.WaitForExit();
    if (process.ExitCode != 0)
        throw new InvalidOperationException($"git {arguments} failed.");
    return output;
}

var corpus = AudioBenchmarkInputs.VerifyCorpus();
string revision = Git("rev-parse HEAD");
bool dirty = Git("status --porcelain").Length != 0;
if (!smoke && !verify && dirty)
    throw new InvalidOperationException("Commit the measured source before a recorded campaign.");
int artifactIndex = Array.IndexOf(args, "--artifacts");
string artifacts = artifactIndex >= 0 ? args[artifactIndex + 1] : "BenchmarkDotNet.Artifacts";
Directory.CreateDirectory(artifacts);
File.WriteAllText(Path.Combine(artifacts, "audio-corpus.json"), JsonSerializer.Serialize(new
{
    Revision = revision,
    Dirty = dirty,
    Smoke = smoke,
    Runtime = RuntimeInformation.FrameworkDescription,
    OS = RuntimeInformation.OSDescription,
    Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    Vector128 = Vector128.IsHardwareAccelerated,
    Vector256 = Vector256.IsHardwareAccelerated,
    Vector512 = Vector512.IsHardwareAccelerated,
    ServerGC = GCSettings.IsServerGC,
    Tensors = "10.0.12",
    BenchmarkDotNet = "0.15.8",
    RelativeErrorTarget = 0.02,
    ErrorConfidence = "99.9%",
    PowerPlan = "Unchanged user plan",
    RuntimeOverrides = new[] { "DOTNET_EnableHWIntrinsic", "DOTNET_TieredCompilation", "DOTNET_TieredPGO",
        "COMPlus_EnableHWIntrinsic", "COMPlus_TieredCompilation", "COMPlus_TieredPGO" }
        .ToDictionary(name => name, Environment.GetEnvironmentVariable),
    Corpus = corpus
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Verified {corpus.Count} corpus entries at {revision}; manifest: {artifacts}");
if (verify)
    return;

var job = (smoke ? Job.Dry : Job.Default)
    .WithRuntime(CoreRuntime.Core10_0)
    .WithPowerPlan(PowerPlan.UserPowerPlan)
    .WithMaxRelativeError(0.02)
    .WithId(smoke ? "SmokeNotEvidence" : "AudioDefault");
var config = ManualConfig.Create(DefaultConfig.Instance)
    .AddJob(job)
    .AddDiagnoser(MemoryDiagnoser.Default)
    .AddExporter(JsonExporter.Full, CsvMeasurementsExporter.Default);
var summaries = BenchmarkSwitcher.FromAssembly(typeof(AudioRmsBenchmarks).Assembly).Run(args, config);
if (summaries.Any(summary => summary.HasCriticalValidationErrors || summary.Reports.Any(report => !report.Success)))
    Environment.ExitCode = 1;
