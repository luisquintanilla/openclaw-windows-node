using System.Buffers.Binary;
using Microsoft.Extensions.AI;
using OpenClaw.Shared.Capabilities;
using OpenClawTray.Services;

#pragma warning disable MEAI001 // Exercise the pinned experimental synthesis contract.

namespace OpenClaw.Tray.Tests;

public sealed class WindowsTextToSpeechClientTests
{
    private static byte[] Wav() => Convert.FromHexString(
        "524946462C00000057415645666D74201000000001000100401F0000803E000002001000646174610800000000000100FFFF0000");

    [Theory]
    [InlineData(null)]
    [InlineData("wav")]
    [InlineData("audio/wav")]
    [InlineData("WAV")]
    public async Task GetAudioAsync_MapsVoiceAndReturnsOwnedPcmWav(string? format)
    {
        var expected = Wav();
        using var cts = new CancellationTokenSource();
        using var client = new WindowsTextToSpeechClient((text, voice, token) =>
        {
            Assert.Equal(" hello ", text);
            Assert.Equal("voice-id", voice);
            Assert.Equal(cts.Token, token);
            return Task.FromResult(expected);
        });
        var options = new TextToSpeechOptions { VoiceId = "voice-id", AudioFormat = format };

        var response = await client.GetAudioAsync(" hello ", options, cts.Token);

        var audio = Assert.IsType<DataContent>(Assert.Single(response.Contents));
        Assert.Equal("audio/wav", audio.MediaType);
        Assert.Equal(expected, audio.Data.ToArray());
        Assert.Equal(8000, BinaryPrimitives.ReadInt32LittleEndian(audio.Data.Span[24..]));
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(audio.Data.Span[22..]));
        Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(audio.Data.Span[34..]));
        Assert.Null(response.RawRepresentation);
        Assert.Null(response.ModelId);
        Assert.Equal("voice-id", options.VoiceId);
        Assert.Equal(format, options.AudioFormat);
    }

    [Fact]
    public async Task GetAudioAsync_DefaultOptionsPassNoExplicitVoice()
    {
        using var client = new WindowsTextToSpeechClient((_, voice, _) =>
        {
            Assert.Null(voice);
            return Task.FromResult(Wav());
        });
        Assert.Single((await client.GetAudioAsync("hello")).Contents);
    }

    [Fact]
    public async Task GetAudioAsync_NullTextFailsBeforeSynthesis()
    {
        using var client = NeverSynthesize();
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetAudioAsync(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public async Task GetAudioAsync_EmptyTextFailsBeforeSynthesis(string text)
    {
        using var client = NeverSynthesize();
        await Assert.ThrowsAsync<ArgumentException>(() => client.GetAudioAsync(text));
    }

    [Fact]
    public async Task GetAudioAsync_DirectCallsPreserveTextLongerThanNodeLimit()
    {
        var input = new string('x', TtsCapability.MaxTextLength + 1);
        var calls = 0;
        using var client = new WindowsTextToSpeechClient((text, _, _) =>
        {
            calls++;
            Assert.Equal(input, text);
            return Task.FromResult(Wav());
        });
        Assert.Single((await client.GetAudioAsync(input)).Contents);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("format")]
    [InlineData("model")]
    [InlineData("language")]
    [InlineData("speed")]
    [InlineData("pitch")]
    [InlineData("volume")]
    [InlineData("raw")]
    [InlineData("additional")]
    public async Task GetAudioAsync_UnsupportedOptionsFailBeforeSynthesis(string option)
    {
        var options = option switch
        {
            "format" => new TextToSpeechOptions { AudioFormat = "mp3" },
            "model" => new TextToSpeechOptions { ModelId = "model" },
            "language" => new TextToSpeechOptions { Language = "en-US" },
            "speed" => new TextToSpeechOptions { Speed = 1f },
            "pitch" => new TextToSpeechOptions { Pitch = 1f },
            "volume" => new TextToSpeechOptions { Volume = 1f },
            "raw" => new TextToSpeechOptions { RawRepresentationFactory = _ => throw new InvalidOperationException("Must not invoke") },
            _ => new TextToSpeechOptions { AdditionalProperties = new() { ["custom"] = true } }
        };
        using var client = NeverSynthesize();
        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetAudioAsync("hello", options));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a wave")]
    public async Task GetAudioAsync_RejectsMissingOrWrongContainer(string? data)
    {
        using var client = new WindowsTextToSpeechClient((_, _, _) =>
            Task.FromResult(data is null ? null! : System.Text.Encoding.UTF8.GetBytes(data)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAudioAsync("hello"));
    }

    [Fact]
    public async Task GetAudioAsync_AlreadyCanceledDoesNotStartSynthesis()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var client = NeverSynthesize();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAudioAsync("hello", cancellationToken: cts.Token));
    }

    [Fact]
    public async Task GetAudioAsync_CancellationDuringSynthesisPropagatesAndReleasesOperation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hold = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = false;
        using var cts = new CancellationTokenSource();
        using var client = new WindowsTextToSpeechClient(async (_, _, token) =>
        {
            entered.SetResult();
            try { return await hold.Task.WaitAsync(token); }
            finally { released = true; }
        });
        var pending = client.GetAudioAsync("hello", cancellationToken: cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(released);
    }

    [Fact]
    public async Task GetAudioAsync_CancellationAfterBackendCompletionDoesNotPublishAudio()
    {
        using var cts = new CancellationTokenSource();
        using var client = new WindowsTextToSpeechClient((_, _, _) =>
        {
            cts.Cancel();
            return Task.FromResult(Wav());
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAudioAsync("hello", cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Dispose_RejectsNewCallsWithoutBlockingOrReleasingActiveOperations()
    {
        var held = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var releases = 0;
        using var client = new WindowsTextToSpeechClient(async (_, _, _) =>
        {
            Interlocked.Increment(ref calls);
            try { return await held.Task; }
            finally { Interlocked.Increment(ref releases); }
        });
        var first = client.GetAudioAsync("first");
        var second = client.GetAudioAsync("second");
        Assert.Equal(2, calls);

        client.Dispose();
        client.Dispose();
        Assert.Equal(0, releases);
        Assert.False(first.IsCompleted);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetAudioAsync("third"));
        held.SetResult(Wav());
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, releases);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetStreamingAudioAsync_YieldsOneCompletedWaveNotIncrementalChunks()
    {
        using var client = new WindowsTextToSpeechClient((_, _, _) => Task.FromResult(Wav()));
        var updates = new List<TextToSpeechResponseUpdate>();
        await foreach (var update in client.GetStreamingAudioAsync("hello"))
            updates.Add(update);

        var completed = Assert.Single(updates);
        Assert.Equal(TextToSpeechResponseUpdateKind.AudioUpdated, completed.Kind);
        Assert.Equal(Wav(), Assert.IsType<DataContent>(Assert.Single(completed.Contents)).Data.ToArray());
    }

    [Fact]
    public async Task GetStreamingAudioAsync_EnumeratorCancellationPublishesNoPartialWave()
    {
        using var cts = new CancellationTokenSource();
        using var client = new WindowsTextToSpeechClient((_, _, _) =>
        {
            cts.Cancel();
            return Task.FromResult(Wav());
        });
        await using var enumerator = client.GetStreamingAudioAsync("hello").GetAsyncEnumerator(cts.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { await enumerator.MoveNextAsync(); });
    }

    [Fact]
    public void GetService_ReturnsOnlyUnkeyedMetadataAndAdapter()
    {
        using var client = NeverSynthesize();
        Assert.Same(client, client.GetService(typeof(ITextToSpeechClient)));
        Assert.Same(client, client.GetService(typeof(WindowsTextToSpeechClient)));
        var metadata = Assert.IsType<TextToSpeechClientMetadata>(client.GetService(typeof(TextToSpeechClientMetadata)));
        Assert.Equal("windows", metadata.ProviderName);
        Assert.Null(metadata.ProviderUri);
        Assert.Null(metadata.DefaultModelId);
        Assert.Null(client.GetService(typeof(IDisposable), "key"));
        Assert.Null(client.GetService(typeof(string)));
        Assert.Throws<ArgumentNullException>(() => client.GetService(null!));
    }

    [Fact]
    public async Task GetAudioAsync_PreservesBackendFailure()
    {
        var failure = new InvalidOperationException("Synthetic backend failure");
        using var client = new WindowsTextToSpeechClient((_, _, _) => Task.FromException<byte[]>(failure));
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAudioAsync("hello")));
    }

    private static WindowsTextToSpeechClient NeverSynthesize() =>
        new((_, _, _) => throw new Xunit.Sdk.XunitException("Unexpected synthesis"));
}
