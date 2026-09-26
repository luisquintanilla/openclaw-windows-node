using System.Buffers.Binary;
using Microsoft.Extensions.AI;
using OpenClaw.Shared;
using OpenClaw.Shared.Audio;
using OpenClaw.Shared.Capabilities;
using OpenClaw.TestSupport;
using OpenClawTray.Services;
using Windows.Media.SpeechSynthesis;
using Xunit.Abstractions;

#pragma warning disable MEAI001 // Exercise the pinned experimental synthesis contract.

namespace OpenClaw.Tray.UITests;

/// <summary>Real service tests. Playback is always replaced; no sound or cloud calls.</summary>
public sealed class WindowsTextToSpeechServiceTests(ITestOutputHelper output)
{
    private static byte[] Wav() => Convert.FromHexString(
        "524946462C00000057415645666D74201000000001000100401F0000803E000002001000646174610800000000000100FFFF0000");

    [Theory]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public async Task SpeakAsync_DirectWindowsPathForwardsEmptyAndWhitespaceText(string text)
    {
        var calls = 0;
        using var adapter = new WindowsTextToSpeechClient((input, _, _) =>
        {
            Assert.Equal(text, input);
            calls++;
            return Task.FromResult(Wav());
        });
        using var fixture = new Fixture(speechClient: adapter);

        var result = await fixture.Service.SpeakAsync(new() { Text = text, Provider = "windows" });

        Assert.True(result.Spoken);
        Assert.Equal(1, calls);
        Assert.Equal(1, fixture.PlayCalls);
        Assert.Equal(Wav(), fixture.Audio);
    }

    [Fact]
    public async Task SpeakAsync_DirectWindowsPathPreservesTextLongerThanNodeLimit()
    {
        var text = new string('x', TtsCapability.MaxTextLength + 1);
        var calls = 0;
        using var adapter = new WindowsTextToSpeechClient((input, _, _) =>
        {
            Assert.Equal(text, input);
            calls++;
            return Task.FromResult(Wav());
        });
        using var fixture = new Fixture(speechClient: adapter);

        var result = await fixture.Service.SpeakAsync(new() { Text = text, Provider = "windows" });

        Assert.True(result.Spoken);
        Assert.Equal(1, calls);
        Assert.Equal(1, fixture.PlayCalls);
        Assert.Equal(Wav(), fixture.Audio);
    }

    [Fact]
    public async Task SpeakAsync_WindowsAwaitsPlaybackAndPreservesCompletionMetadata()
    {
        var playback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var fixture = new Fixture((audio, type, interrupt, _) =>
        {
            Assert.Equal(Wav(), audio);
            Assert.Equal("audio/wav", type);
            Assert.True(interrupt);
            return playback.Task;
        });
        var pending = fixture.Service.SpeakAsync(new() { Text = "hello", Provider = "windows", VoiceId = "explicit", Interrupt = true });
        Assert.False(pending.IsCompleted);
        Assert.Equal("hello", fixture.Client.Text);
        Assert.Equal("explicit", fixture.Client.Options?.VoiceId);
        Assert.Null(fixture.Client.Options?.ModelId);
        playback.SetResult();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(result.Spoken);
        Assert.Equal("windows", result.Provider);
        Assert.Equal("windows", result.RequestedProvider);
        Assert.False(result.FellBack);
        Assert.Equal("audio/wav", result.ContentType);
        Assert.InRange(Assert.IsType<int>(result.DurationMs), 0, int.MaxValue);
    }

    [Theory]
    [InlineData("piper")]
    [InlineData("elevenlabs")]
    [InlineData("minimax")]
    public async Task SpeakAsync_UnreadyConfiguredProviderFallsBackOfflineAndDropsProviderSpecificOptions(string provider)
    {
        using var fixture = new Fixture();
        fixture.Settings.TtsProvider = provider;
        var result = await fixture.Service.SpeakAsync(new()
        {
            Text = "hello", VoiceId = "en_US-amy-low", Model = "provider-model", Interrupt = true
        });
        Assert.Equal(provider, result.RequestedProvider);
        Assert.Equal("windows", result.Provider);
        Assert.True(result.FellBack);
        Assert.Null(fixture.Client.Options?.VoiceId);
        Assert.Null(fixture.Client.Options?.ModelId);
        Assert.Equal(1, fixture.PlayCalls);
        Assert.True(fixture.Interrupt);
    }

    [Theory]
    [InlineData("piper")]
    [InlineData("elevenlabs")]
    [InlineData("minimax")]
    [InlineData("unknown")]
    public async Task SpeakAsync_ExplicitUnavailableProviderNeverCallsWindowsOrPlayback(string provider)
    {
        using var fixture = new Fixture();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.SpeakAsync(new() { Text = "hello", Provider = provider }));
        Assert.Null(fixture.Client.Text);
        Assert.Equal(0, fixture.PlayCalls);
    }

    [Fact]
    public async Task SpeakAsync_SynthesisFailureDoesNotStartPlaybackOrFallback()
    {
        using var fixture = new Fixture();
        fixture.Client.Synthesis = (_, _, _) => Task.FromException<TextToSpeechResponse>(new InvalidOperationException("Synthetic failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.SpeakAsync(new() { Text = "hello", Provider = "windows" }));
        Assert.Equal(0, fixture.PlayCalls);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("text")]
    [InlineData("mp3")]
    [InlineData("multiple")]
    [InlineData("invalid-wav")]
    public async Task SpeakAsync_RejectsMalformedSynthesisResponseBeforePlayback(string shape)
    {
        using var fixture = new Fixture();
        fixture.Client.Synthesis = (_, _, _) => Task.FromResult(shape switch
        {
            "null" => null!,
            "empty" => new TextToSpeechResponse(),
            "text" => new TextToSpeechResponse([new TextContent("not audio")]),
            "mp3" => new TextToSpeechResponse([new DataContent(Wav(), "audio/mpeg")]),
            "multiple" => new TextToSpeechResponse([new DataContent(Wav(), "audio/wav"), new DataContent(Wav(), "audio/wav")]),
            _ => new TextToSpeechResponse([new DataContent(new byte[] { 1, 2, 3 }, "audio/wav")])
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.SpeakAsync(new() { Text = "hello", Provider = "windows" }));
        Assert.Equal(0, fixture.PlayCalls);
    }

    [Fact]
    public async Task SpeakAsync_CancellationAfterSynthesisPreventsPlayback()
    {
        using var fixture = new Fixture();
        using var cts = new CancellationTokenSource();
        fixture.Client.Synthesis = (_, _, _) =>
        {
            cts.Cancel();
            return Task.FromResult(new TextToSpeechResponse([new DataContent(Wav(), "audio/wav")]));
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.SpeakAsync(new() { Text = "hello", Provider = "windows" }, cts.Token));
        Assert.Equal(0, fixture.PlayCalls);
    }

    [Fact]
    public async Task SpeakAsync_PlaybackCancellationDoesNotReportSpokenOrDisposeClient()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        using var fixture = new Fixture(async (_, _, _, token) =>
        {
            Assert.Equal(cts.Token, token);
            entered.SetResult();
            await hold.Task.WaitAsync(token);
        });
        var pending = fixture.Service.SpeakAsync(new() { Text = "hello", Provider = "windows" }, cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(0, fixture.Client.DisposeCalls);
    }

    [Fact]
    public async Task SpeakAsync_NullArgumentsFailExplicitly()
    {
        using var fixture = new Fixture();
        await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Service.SpeakAsync(null!));
        Assert.Equal(0, fixture.PlayCalls);
    }

    [Fact]
    public void Dispose_ServiceOwnsInjectedClient()
    {
        using var fixture = new Fixture();
        fixture.Service.Dispose();
        Assert.Equal(1, fixture.Client.DisposeCalls);
    }

    [Fact]
    [Trait("Category", "NativeWindowsSpeech")]
    public async Task NativeWindows_DefaultAndStaleConfiguredVoiceReturnSilentPcmWav()
    {
        using var fixture = new Fixture(useNativeClient: true);
        await fixture.Service.SpeakAsync(new() { Text = "Offline synthesis test.", Provider = "windows" });
        AssertPcmWave(fixture.Audio!);
        fixture.Settings.TtsWindowsVoiceId = "openclaw-test-missing-voice";
        await fixture.Service.SpeakAsync(new() { Text = "Offline fallback test.", Provider = "windows" });
        AssertPcmWave(fixture.Audio!);
        Assert.Equal(2, fixture.PlayCalls);
    }

    [Fact]
    [Trait("Category", "NativeWindowsSpeech")]
    public async Task NativeWindows_UnreadyDefaultPiperUsesRealAdapterAndSilentOfflineFallback()
    {
        using var fixture = new Fixture(useNativeClient: true);
        fixture.Settings.TtsProvider = "piper";
        var result = await fixture.Service.SpeakAsync(new()
        {
            Text = "Offline provider fallback test.", VoiceId = "en_US-amy-low", Model = "piper-model"
        });
        Assert.Equal("piper", result.RequestedProvider);
        Assert.Equal("windows", result.Provider);
        Assert.True(result.FellBack);
        Assert.True(result.Spoken);
        Assert.Equal("audio/wav", result.ContentType);
        Assert.Equal(1, fixture.PlayCalls);
        AssertPcmWave(fixture.Audio!);
    }

    [Fact]
    [Trait("Category", "NativeWindowsSpeech")]
    public async Task NativeWindows_ExplicitInstalledVoiceWorksAndMissingVoiceStaysStrict()
    {
        using var fixture = new Fixture(useNativeClient: true);
        var installed = SpeechSynthesizer.DefaultVoice;
        Assert.NotNull(installed);
        await fixture.Service.SpeakAsync(new() { Text = "Offline voice test.", Provider = "windows", VoiceId = installed.Id });
        AssertPcmWave(fixture.Audio!);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.SpeakAsync(new() { Text = "hello", Provider = "windows", VoiceId = "openclaw-test-missing-voice" }));
        Assert.Equal(1, fixture.PlayCalls);
    }

    private void AssertPcmWave(byte[] wav)
    {
        Assert.Equal("RIFF"u8.ToArray(), wav[..4]);
        Assert.Equal("WAVE"u8.ToArray(), wav[8..12]);
        Assert.Equal(wav.Length - 8, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(4)));
        var foundFormat = false;
        var foundAudio = false;
        for (var offset = 12; offset + 8 <= wav.Length;)
        {
            var size = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(offset + 4));
            Assert.InRange(size, 0, wav.Length - offset - 8);
            if (wav.AsSpan(offset, 4).SequenceEqual("fmt "u8))
            {
                Assert.True(size >= 16);
                Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(offset + 8)));
                Assert.InRange(BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(offset + 10)), (short)1, (short)2);
                Assert.InRange(BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(offset + 12)), 8000, 192000);
                Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(offset + 22)));
                output.WriteLine(
                    "Native Windows WAV: bytes={0}, format=PCM, channels={1}, sampleRate={2}, bitsPerSample={3}. Playback replaced.",
                    wav.Length,
                    BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(offset + 10)),
                    BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(offset + 12)),
                    BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(offset + 22)));
                foundFormat = true;
            }
            if (wav.AsSpan(offset, 4).SequenceEqual("data"u8))
            {
                Assert.True(size > 0);
                Assert.Contains(wav.AsSpan(offset + 8, size).ToArray(), sample => sample != 0);
                foundAudio = true;
            }
            offset += 8 + size + (size & 1);
        }
        Assert.True(foundFormat);
        Assert.True(foundAudio);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TempDirectory _temp = new();
        public SettingsManager Settings { get; }
        public StubSpeechClient Client { get; } = new();
        public TextToSpeechService Service { get; }
        public int PlayCalls { get; private set; }
        public bool Interrupt { get; private set; }
        public byte[]? Audio { get; private set; }

        public Fixture(
            Func<byte[], string, bool, CancellationToken, Task>? playback = null,
            bool useNativeClient = false,
            ITextToSpeechClient? speechClient = null)
        {
            Settings = new SettingsManager(_temp.Path)
            {
                TtsProvider = "windows",
                TtsPiperVoiceId = "en_US-amy-low",
                TtsElevenLabsApiKey = "",
                TtsMiniMaxApiKey = ""
            };
            Service = new TextToSpeechService(NullLogger.Instance, Settings,
                new ElevenLabsTextToSpeechClient(), new MiniMaxTextToSpeechClient(),
                useNativeClient ? null : speechClient ?? Client,
                async (audio, type, interrupt, token) =>
                {
                    PlayCalls++;
                    Interrupt = interrupt;
                    Audio = audio;
                    if (playback is not null)
                        await playback(audio, type, interrupt, token);
                },
                new PiperVoiceManager(_temp.Path, NullLogger.Instance));
        }

        public void Dispose()
        {
            Service.Dispose();
            _temp.Dispose();
        }
    }

    private sealed class StubSpeechClient : ITextToSpeechClient
    {
        public string? Text { get; private set; }
        public TextToSpeechOptions? Options { get; private set; }
        public int DisposeCalls { get; private set; }
        public Func<string, TextToSpeechOptions?, CancellationToken, Task<TextToSpeechResponse>> Synthesis { get; set; } =
            (_, _, _) => Task.FromResult(new TextToSpeechResponse([new DataContent(Wav(), "audio/wav")]));

        public Task<TextToSpeechResponse> GetAudioAsync(string text, TextToSpeechOptions? options = null, CancellationToken cancellationToken = default)
        {
            Text = text;
            Options = options;
            return Synthesis(text, options, cancellationToken);
        }

        public IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(string text, TextToSpeechOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The service must request a complete WAV.");
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() => DisposeCalls++;
    }
}
