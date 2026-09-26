using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenClaw.Shared.Audio;
using Whisper.net;
using Whisper.net.LibraryLoader;
using Xunit.Abstractions;

#pragma warning disable MEAI001 // Verify binary compatibility with the pinned speech contract.

namespace OpenClaw.Shared.Tests;

/// <summary>
/// Opt-in, offline proof against two preverified public assets. Never downloads,
/// captures audio, plays sound, or logs transcripts. See WINDOWS_NODE_TESTING.md.
/// </summary>
public sealed class SpeechToTextNativeProofTests(ITestOutputHelper output)
{
    [WhisperNativeProofFact]
    public async Task Native_ServiceMatchesFrozenBaselineAndPreservesOwnership()
    {
        var assetDirectory = Environment.GetEnvironmentVariable("OPENCLAW_WHISPER_PROOF_ASSETS");
        Assert.False(string.IsNullOrWhiteSpace(assetDirectory), "Set the approved public asset directory.");
        var modelPath = Path.Combine(assetDirectory!, "ggml-tiny.bin");
        var fixtureBytes = File.ReadAllBytes(Path.Combine(assetDirectory!, "jfk.wav"));
        Assert.Equal(77_691_713, new FileInfo(modelPath).Length);
        using (var model = File.OpenRead(modelPath))
            Assert.Equal("be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21",
                Convert.ToHexStringLower(SHA256.HashData(model)));
        Assert.Equal(352078, fixtureBytes.Length);
        var blobBytes = Encoding.UTF8.GetBytes($"blob {fixtureBytes.Length}\0").Concat(fixtureBytes).ToArray();
        Assert.Equal("3184d372cd2f8b804d3a540c70ec50d927b335d2", Convert.ToHexStringLower(SHA1.HashData(blobBytes)));
        output.WriteLine($"fixtureSha256={Convert.ToHexStringLower(SHA256.HashData(fixtureBytes))}");

        var samples = ReadPublicPcm16Fixture(fixtureBytes);
        var sampleBytes = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, sampleBytes, 0, sampleBytes.Length);
        output.WriteLine($"sampleCount={samples.Length}; floatBytesSha256={Convert.ToHexStringLower(SHA256.HashData(sampleBytes))}");
        output.WriteLine($"whisperAssembly={typeof(WhisperFactory).Assembly.GetName().Version}; meaiAssembly={typeof(ISpeechToTextClient).Assembly.GetName().Version}");

        using var baselineFactory = WhisperFactory.FromPath(modelPath);
        await using var service = new SpeechToTextService(NullLogger.Instance);
        await service.LoadModelAsync(modelPath);
        Assert.True(service.IsModelLoaded);
        Assert.NotNull(RuntimeOptions.LoadedLibrary);
        output.WriteLine($"backend={RuntimeOptions.LoadedLibrary}; runtime={WhisperFactory.GetRuntimeInfo()}; threads={Math.Max(1, Environment.ProcessorCount / 2)}");
        using (var process = Process.GetCurrentProcess())
        {
            var modules = process.Modules.Cast<ProcessModule>()
                .Where(module => module.ModuleName.EndsWith("whisper.dll", StringComparison.OrdinalIgnoreCase))
                .OrderBy(module => module.ModuleName).ToArray();
            Assert.Contains(modules, module => module.ModuleName.Equals("whisper.dll", StringComparison.OrdinalIgnoreCase));
            foreach (var module in modules)
            {
                using var binary = File.OpenRead(module.FileName);
                output.WriteLine($"nativeModule={module.ModuleName}; sha256={Convert.ToHexStringLower(SHA256.HashData(binary))}");
            }
        }

        foreach (var language in new[] { "en-US", "auto" })
        {
            string? previousHash = null;
            for (var repeat = 0; repeat < 2; repeat++)
            {
                var watch = Stopwatch.StartNew();
                var baseline = await FrozenBaselineAsync(baselineFactory, samples, language);
                var baselineMs = watch.ElapsedMilliseconds;
                watch.Restart();
                var candidate = await service.TranscribeAsync(samples, language);
                var candidateMs = watch.ElapsedMilliseconds;
                Assert.NotEmpty(baseline);
                var baselineHash = SegmentHash(baseline);
                var candidateHash = SegmentHash(candidate);
                output.WriteLine($"language={language}; repeat={repeat + 1}; segments={candidate.Count}; baselineHash={baselineHash}; candidateHash={candidateHash}; baselineMs={baselineMs}; candidateMs={candidateMs}");
                // Hash the complete ordered text/timestamp/language contract so assertion
                // failures report no transcript content. Do not relax native variability.
                Assert.Equal(baselineHash, candidateHash);
                if (previousHash != null) Assert.Equal(previousHash, candidateHash);
                previousHash = candidateHash;
            }
        }

        // The upstream adapter owns its factory, but not the caller's WAV stream.
        using (ISpeechToTextClient adapter = new WhisperSpeechToTextClient(modelPath))
        using (var callerStream = FrozenPcmToWav(samples))
        {
            var options = new SpeechToTextOptions { SpeechLanguage = "en", SpeechSampleRate = 16000 }
                .WithThreads(Math.Max(1, Environment.ProcessorCount / 2));
            var segmentCount = 0;
            await foreach (var _ in adapter.GetStreamingTextAsync(callerStream, options)) segmentCount++;
            Assert.True(segmentCount > 0);
            Assert.True(callerStream.CanRead);
            callerStream.Position = 0;
            Assert.Equal((byte)'R', callerStream.ReadByte());
            try
            {
                var metadata = adapter.GetService(typeof(SpeechToTextClientMetadata));
                output.WriteLine($"adapterMetadataAvailable={metadata is SpeechToTextClientMetadata}");
            }
            catch (NotImplementedException)
            {
                output.WriteLine("adapterMetadata=NotImplementedException (observed limitation, not an acceptance requirement)");
            }
        }

        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.TranscribeAsync(samples, cancellationToken: cancellation.Token));
            Assert.Equal(cancellation.Token, error.CancellationToken);
        }
        await service.UnloadModelAsync();
        Assert.False(service.IsModelLoaded);
        await service.LoadModelAsync(modelPath);
        await service.UnloadModelAsync(); // Also exercise eager factory cleanup before the adapter is used.
        Assert.False(service.IsModelLoaded);
        output.WriteLine("cancellationScope=preCanceledAdmissionOnly; nativeMidFlightAbort=unverified; callerStream=leftOpen; modelReloadWithoutInference=completed");
    }

    private static string SegmentHash(IEnumerable<TranscriptionResult> results) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            results.Select(s => new { s.Text, StartTicks = s.Start.Ticks, EndTicks = s.End.Ticks, s.Language }))));

    private static float[] ReadPublicPcm16Fixture(byte[] bytes)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes));
        Assert.Equal("RIFF", Encoding.ASCII.GetString(reader.ReadBytes(4)));
        reader.ReadInt32();
        Assert.Equal("WAVE", Encoding.ASCII.GetString(reader.ReadBytes(4)));
        var formatVerified = false;
        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
        {
            var name = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var size = reader.ReadInt32();
            Assert.InRange(size, 0, bytes.Length);
            var next = reader.BaseStream.Position + size + (size & 1);
            if (name == "fmt ")
            {
                Assert.Equal(1, reader.ReadInt16());
                Assert.Equal(1, reader.ReadInt16());
                Assert.Equal(16000, reader.ReadInt32());
                Assert.Equal(32000, reader.ReadInt32());
                Assert.Equal(2, reader.ReadInt16());
                Assert.Equal(16, reader.ReadInt16());
                formatVerified = true;
            }
            else if (name == "data")
            {
                Assert.True(formatVerified);
                Assert.Equal(0, size % 2);
                var samples = new float[size / 2];
                for (var i = 0; i < samples.Length; i++) samples[i] = reader.ReadInt16() / 32768f;
                return samples;
            }
            reader.BaseStream.Position = next;
        }
        throw new InvalidDataException("The pinned public fixture has no PCM data.");
    }

    // Independent copy of the original 273b018 service's conversion/processor/mapping.
    // Never call candidate conversion, normalization, or response-mapping helpers here.
    private static async Task<List<TranscriptionResult>> FrozenBaselineAsync(
        WhisperFactory factory, float[] samples, string language)
    {
        var trimmed = language.Trim();
        var dash = trimmed.IndexOf('-');
        var normalized = (dash >= 0 ? trimmed[..dash] : trimmed).ToLowerInvariant();
        if (normalized.Length != 2 || normalized[0] is < 'a' or > 'z' || normalized[1] is < 'a' or > 'z')
            normalized = "auto";
        using var processor = factory.CreateBuilder().WithLanguage(normalized)
            .WithThreads(Math.Max(1, Environment.ProcessorCount / 2)).Build();
        using var stream = FrozenPcmToWav(samples);
        var result = new List<TranscriptionResult>();
        await foreach (var segment in processor.ProcessAsync(stream))
        {
            var text = segment.Text?.Trim();
            if (!string.IsNullOrEmpty(text))
                result.Add(new TranscriptionResult
                {
                    Text = text, Start = segment.Start, End = segment.End, Language = normalized
                });
        }
        return result;
    }

    private static MemoryStream FrozenPcmToWav(float[] samples)
    {
        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("RIFF"u8);
        writer.Write(36 + samples.Length * 2);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(16000);
        writer.Write(32000);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(samples.Length * 2);
        foreach (var sample in samples) writer.Write((short)(Math.Clamp(sample, -1.0f, 1.0f) * 32767));
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    private sealed class WhisperNativeProofFactAttribute : FactAttribute
    {
        public WhisperNativeProofFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("OPENCLAW_RUN_WHISPER_PROOF") != "1")
                Skip = "Opt-in public-fixture native proof. Set OPENCLAW_RUN_WHISPER_PROOF=1 and OPENCLAW_WHISPER_PROOF_ASSETS.";
        }
    }
}

#pragma warning restore MEAI001
