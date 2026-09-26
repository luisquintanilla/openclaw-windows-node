using System.Reflection;
using NAudio.Wave;
using OpenClaw.Audio.Testing;
using OpenClaw.Shared;
using OpenClaw.Shared.Audio;
using OpenClawTray.Services;

namespace OpenClaw.Tray.UITests;

public sealed class AudioPipelineNumericsTests
{
    private const int ChunkSize = 512;

    [Fact]
    public async Task EmptyMutedCancelledAndStaleCallbacks_EmitNoNumericalEvents()
    {
        var first = new Capture();
        var second = new Capture();
        var captures = new Queue<Capture>([first, second]);
        using var cancellation = new CancellationTokenSource();
        await using var pipeline = CreatePipeline(() => captures.Dequeue());
        var levels = new List<float>();
        var voices = new List<VadEvent>();
        pipeline.AudioLevelChanged += levels.Add;
        pipeline.VoiceActivityChanged += voices.Add;

        await pipeline.StartAsync(new AudioPipelineOptions(), cancellation.Token);
        first.Emit([]);
        pipeline.IsMuted = true;
        first.Emit(Enumerable.Repeat(0.1f, ChunkSize).ToArray());
        pipeline.IsMuted = false;
        cancellation.Cancel();
        first.Emit(Enumerable.Repeat(0.1f, ChunkSize).ToArray());
        await pipeline.StopAsync();
        first.EmitLate(Enumerable.Repeat(0.1f, ChunkSize).ToArray());
        await pipeline.StartAsync(new AudioPipelineOptions());
        first.EmitLate(Enumerable.Repeat(0.1f, ChunkSize).ToArray());

        Assert.Empty(levels);
        Assert.Empty(voices);
        second.Emit(new float[ChunkSize]);
        Assert.Equal([0f], levels);
        Assert.Empty(voices);
    }

    [Theory]
    [InlineData(false, 7)]
    [InlineData(true, 7)]
    [InlineData(false, 480)]
    [InlineData(true, 480)]
    [InlineData(false, 960)]
    [InlineData(true, 960)]
    [InlineData(false, 1600)]
    [InlineData(true, 1600)]
    public async Task FixedCapture_PreservesPostGainSampleBitsAndMeter_ButBypassesVad(bool pcm16, int length)
    {
        var capture = new Capture(pcm16);
        using var cancellation = new CancellationTokenSource();
        await using var pipeline = CreatePipeline(() => capture);
        var levels = new List<float>();
        var voices = new List<VadEvent>();
        pipeline.AudioLevelChanged += levels.Add;
        pipeline.VoiceActivityChanged += voices.Add;
        var listening = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pipeline.StateChanged += state =>
        {
            if (state == AudioPipelineState.Listening)
                listening.TrySetResult();
        };
        float[] pattern = pcm16
            ? [-1, -0.2f, -0.01f, 0, 0.01f, 0.2f, 1]
            : [-1, -0.2f, -0.01f, -0f, 0f, 0.01f, 0.2f, 1, float.PositiveInfinity];
        var input = Enumerable.Range(0, length).Select(i => pattern[i % pattern.Length]).ToArray();
        var expected = capture.RoundTrip(input);
        AudioNumericsReference.ApplyGain(expected);
        var resultTask = pipeline.CaptureFixedDurationAsync(10_000, cancellation.Token);
        await listening.Task.WaitAsync(TimeSpan.FromSeconds(5));
        capture.Emit([]);
        Assert.Empty(levels);
        capture.Emit(input);
        cancellation.Cancel();
        var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(expected.Select(BitConverter.SingleToInt32Bits), result.Select(BitConverter.SingleToInt32Bits));
        Assert.Empty(voices);
        Assert.InRange(MathF.Abs(Assert.Single(levels) -
            Math.Clamp(AudioNumericsReference.CalculateRms(expected) * 3f, 0f, 1f)), 0f, 1f / 1024);
        Assert.Equal(AudioPipelineState.Stopped, pipeline.State);
    }

    [Theory]
    [InlineData(160, false)]
    [InlineData(480, false)]
    [InlineData(512, false)]
    [InlineData(160, true)]
    [InlineData(512, true)]
    public async Task HysteresisAndSegmentation_MatchScalarTraceAtExactConsumedChunks(int callbackSize, bool pcm16)
    {
        var capture = new Capture(pcm16);
        await using var pipeline = CreatePipeline(() => capture);
        // Three silent chunks terminate an utterance. Nine speech chunks are
        // rejected; ten qualify, regardless of the prebuffer or short gaps.
        const int silenceLimit = 3;
        var amplitudes = new List<float>();
        amplitudes.AddRange(Enumerable.Repeat(0f, 12));
        amplitudes.AddRange(Enumerable.Repeat(0.02f, 10));
        amplitudes.AddRange([0f, 0.003f, 0f, 0f, 0f]);
        amplitudes.AddRange(Enumerable.Repeat(0.02f, 9));
        amplitudes.AddRange([0f, 0f, 0f]);
        foreach (float threshold in new[] { 0.03f, 0.008f })
        {
            float amplitude = threshold / 5f;
            // Actual post-conversion/post-gain values decide the reference,
            // not the nominal pre-gain threshold.
            amplitudes.AddRange([0.02f, MathF.BitDecrement(amplitude), amplitude,
                MathF.BitIncrement(amplitude), amplitude, 0f, 0f, 0f]);
        }
        var input = amplitudes.SelectMany(value => Enumerable.Repeat(value, ChunkSize)).ToArray();
        var postGain = capture.RoundTrip(input);
        AudioNumericsReference.ApplyGain(postGain);
        var expected = ReferenceTrace(postGain, silenceLimit);
        var actual = new List<(int Chunk, bool Speaking, float Probability)>();
        var eligibility = new List<(int Chunk, bool Eligible)>();
        var consumed = 0;
        pipeline.VoiceActivityChanged += voice =>
            actual.Add((consumed / ChunkSize, voice.IsSpeaking, voice.Probability));
        pipeline.DiagnosticMessage += message =>
        {
            if (message.StartsWith("Transcribing ", StringComparison.Ordinal))
                eligibility.Add((consumed / ChunkSize, true));
            else if (message == "Speak now: I'm listening")
                eligibility.Add((consumed / ChunkSize, false));
        };
        var levels = new List<float>();
        pipeline.AudioLevelChanged += levels.Add;
        await pipeline.StartAsync(new AudioPipelineOptions { SilenceTimeoutSeconds = 0.1f });
        for (int offset = 0; offset < input.Length; offset += callbackSize)
        {
            int length = Math.Min(callbackSize, input.Length - offset);
            consumed += length;
            capture.Emit(input.AsSpan(offset, length).ToArray());
            float meter = Math.Clamp(AudioNumericsReference.CalculateRms(postGain.AsSpan(offset, length)) * 3f, 0f, 1f);
            Assert.InRange(MathF.Abs(levels[^1] - meter), 0f, 1f / 1024);
        }

        Assert.Equal(expected.Events.Select(x => (x.Chunk, x.Speaking)), actual.Select(x => (x.Chunk, x.Speaking)));
        Assert.Equal(expected.Eligibility, eligibility);
        Assert.Contains(eligibility, x => x.Eligible);
        Assert.Contains(eligibility, x => !x.Eligible);
        for (int i = 0; i < actual.Count; i++)
            Assert.Equal(BitConverter.SingleToInt32Bits(expected.Events[i].Probability),
                BitConverter.SingleToInt32Bits(actual[i].Probability));
    }

    [Fact]
    public async Task Prebuffer_OnsetPreservesTenChunksInOriginalSampleOrder()
    {
        var capture = new Capture();
        await using var pipeline = CreatePipeline(() => capture);
        await pipeline.StartAsync(new AudioPipelineOptions());
        var expected = new List<float>();
        for (int i = 0; i < 12; i++)
        {
            var chunk = Enumerable.Repeat(i * 0.0001f, ChunkSize).ToArray();
            capture.Emit(chunk);
            AudioNumericsReference.ApplyGain(chunk);
            if (i >= 2)
                expected.AddRange(chunk);
        }
        var onset = Enumerable.Repeat(0.02f, ChunkSize).ToArray();
        capture.Emit(onset);
        AudioNumericsReference.ApplyGain(onset);
        expected.AddRange(onset);

        // Observation only: model-free public events cannot expose buffered
        // samples. Do not add a production transcription seam for this test.
        var field = typeof(AudioPipeline).GetField("_speechBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        var buffer = Assert.IsType<List<float>>(field.GetValue(pipeline));
        Assert.Equal(expected.Select(BitConverter.SingleToInt32Bits), buffer.Select(BitConverter.SingleToInt32Bits));
    }

    [Fact]
    public async Task NaNInput_EmitsNaNMeterAndDoesNotStartSpeech()
    {
        var capture = new Capture();
        await using var pipeline = CreatePipeline(() => capture);
        var levels = new List<float>();
        var voices = new List<VadEvent>();
        pipeline.AudioLevelChanged += levels.Add;
        pipeline.VoiceActivityChanged += voices.Add;
        await pipeline.StartAsync(new AudioPipelineOptions());
        var samples = Enumerable.Repeat(0.1f, ChunkSize).ToArray();
        samples[ChunkSize / 2] = float.NaN;
        capture.Emit(samples);
        Assert.True(float.IsNaN(Assert.Single(levels)));
        Assert.Empty(voices);
    }

    private static (List<(int Chunk, bool Speaking, float Probability)> Events,
        List<(int Chunk, bool Eligible)> Eligibility) ReferenceTrace(float[] samples, int silenceLimit)
    {
        var events = new List<(int, bool, float)>();
        var eligibility = new List<(int, bool)>();
        bool speaking = false;
        int silence = 0, speech = 0;
        for (int offset = 0; offset < samples.Length; offset += ChunkSize)
        {
            float energy = AudioNumericsReference.CalculateRms(samples.AsSpan(offset, ChunkSize));
            int chunk = offset / ChunkSize + 1;
            if (energy >= (speaking ? 0.008f : 0.03f))
            {
                if (!speaking)
                {
                    events.Add((chunk, true, energy));
                    speech = 0;
                }
                speaking = true;
                silence = 0;
                speech++;
            }
            else if (speaking && ++silence >= silenceLimit)
            {
                speaking = false;
                events.Add((chunk, false, energy));
                eligibility.Add((chunk, speech >= 10));
            }
        }
        return (events, eligibility);
    }

    private static AudioPipeline CreatePipeline(Func<IAudioCapture> factory) => new(
        NullLogger.Instance, new SpeechToTextService(NullLogger.Instance), factory,
        static (_, token) => Task.Delay(Timeout.Infinite, token), TimeSpan.FromSeconds(5), () => "No audio");

    private sealed class Capture(bool pcm16 = false) : IAudioCapture
    {
        private EventHandler<WaveInEventArgs>? _handler;
        private EventHandler<WaveInEventArgs>? _lastHandler;
        public WaveFormat WaveFormat { get; } = pcm16 ? new(16000, 16, 1) : WaveFormat.CreateIeeeFloatWaveFormat(16000, 1);
        public event EventHandler<WaveInEventArgs>? DataAvailable
        {
            add { _handler += value; _lastHandler = value; }
            remove { _handler -= value; }
        }
        public event EventHandler<StoppedEventArgs>? RecordingStopped;
        public void StartRecording() { }
        public void StopRecording() => RecordingStopped?.Invoke(this, new StoppedEventArgs());
        public void Dispose() { }
        public void Emit(float[] samples) => EmitTo(_handler, samples);
        public void EmitLate(float[] samples) => EmitTo(_lastHandler, samples);
        public float[] RoundTrip(float[] samples) => pcm16
            ? samples.Select(value => EncodePcm16(value) / 32768f).ToArray()
            : (float[])samples.Clone();
        private static short EncodePcm16(float value) => (short)Math.Clamp((int)(value * 32768f), short.MinValue, short.MaxValue);
        private void EmitTo(EventHandler<WaveInEventArgs>? handler, float[] samples)
        {
            byte[] bytes = pcm16
                ? samples.SelectMany(value => BitConverter.GetBytes(EncodePcm16(value))).ToArray()
                : samples.SelectMany(BitConverter.GetBytes).ToArray();
            handler?.Invoke(this, new WaveInEventArgs(bytes, bytes.Length));
        }
    }
}
