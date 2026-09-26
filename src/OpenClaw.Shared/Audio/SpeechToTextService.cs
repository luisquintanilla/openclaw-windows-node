using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Whisper.net;

#pragma warning disable MEAI001 // Experimental speech contract, pinned to Abstractions 10.9.0.

namespace OpenClaw.Shared.Audio;

/// <summary>
/// Consumes Whisper.net's speech client while owning model loading and PCM conversion.
/// Transcription, model replacement, unloading, and disposal share one gate.
/// </summary>
public sealed class SpeechToTextService : IDisposable, IAsyncDisposable
{
    private readonly IOpenClawLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<string, (IDisposable Model, Func<ISpeechToTextClient> CreateClient)> _createModel;
    private IDisposable? _factory;
    private ISpeechToTextClient? _client;
    private string? _loadedModelPath;
    private bool _disposed;

    public bool IsModelLoaded => Volatile.Read(ref _client) != null;
    public string? LoadedModelPath => Volatile.Read(ref _loadedModelPath);

    public SpeechToTextService(IOpenClawLogger logger)
    {
        _logger = logger;
        _createModel = CreateWhisperClient;
    }

    // The service owns both resources. The seam does not register alternative engines.
    internal SpeechToTextService(IOpenClawLogger logger,
        Func<string, (IDisposable Model, Func<ISpeechToTextClient> CreateClient)> createModel)
    {
        _logger = logger;
        _createModel = createModel;
    }

    private static (IDisposable Model, Func<ISpeechToTextClient> CreateClient) CreateWhisperClient(string modelPath)
    {
        // Preserve eager loading/readiness rather than the adapter's lazy path constructor.
        var factory = WhisperFactory.FromPath(modelPath);
        return (factory, () => new WhisperSpeechToTextClient(() => factory));
    }

    /// <summary>Load a model when idle. Use LoadModelAsync if transcription may be active.</summary>
    public void LoadModel(string modelPath)
    {
        EnterQuiescentLifecycle();
        try
        {
            LoadModelCore(modelPath);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Wait asynchronously for active transcription, then load the model off the caller context.</summary>
    public async Task LoadModelAsync(string modelPath, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadModelCore(modelPath);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void LoadModelCore(string modelPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!System.IO.File.Exists(modelPath))
            throw new System.IO.FileNotFoundException($"Whisper model not found: {modelPath}");

        ReleaseModel();
        try
        {
            var (factory, createClient) = _createModel(modelPath);
            _factory = factory;
            var client = createClient();
            _loadedModelPath = modelPath;
            _client = client;
        }
        catch
        {
            try
            {
                ReleaseModel();
            }
            catch (Exception cleanupError)
            {
                _logger.Error("Whisper model cleanup failed after loading failure.", cleanupError);
            }
            throw;
        }
        _logger.Info($"Whisper model loaded: {modelPath}");
    }

    /// <summary>Unload an idle model. Use UnloadModelAsync if transcription may be active.</summary>
    public void UnloadModel()
    {
        EnterQuiescentLifecycle();
        try
        {
            UnloadModelCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Wait asynchronously for active transcription before freeing the model.</summary>
    public async Task UnloadModelAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            UnloadModelCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    private void UnloadModelCore()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ReleaseModel();
        _logger.Info("Whisper model unloaded");
    }

    /// <summary>
    /// Transcribe raw 16 kHz mono PCM float samples.
    /// Returns all detected segments.
    /// </summary>
    public async Task<List<TranscriptionResult>> TranscribeAsync(
        float[] samples,
        string language = "auto",
        CancellationToken cancellationToken = default)
    {
        // The pinned adapter captures its starting context internally. Always leave
        // the UI context before entering it, including uncontended admission, so
        // async lifecycle and transcription never depend on a blocked UI continuation.
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var client = _client ??
                throw new InvalidOperationException("No Whisper model is loaded. Call LoadModel first.");
            cancellationToken.ThrowIfCancellationRequested();
            // Whisper.net's WithLanguage expects either "auto" or a 2-letter
            // ISO 639-1 code. The capability validator accepts the broader
            // BCP-47 shape ("en-US", "zh-Hans-CN") because that's what the
            // public docs advertise; normalize down here so Whisper actually
            // sees something it understands.
            var whisperLang = NormalizeForWhisper(language);
            var options = new SpeechToTextOptions
                {
                    SpeechLanguage = whisperLang,
                    SpeechSampleRate = 16000
                    // TextLanguage must stay unset: Whisper interprets any value as translation.
                }
                .WithThreads(Math.Max(1, Environment.ProcessorCount / 2));

            using var wavStream = PcmToWavStream(samples, 16000);

            var results = new List<TranscriptionResult>();
            // In Whisper.net 1.9.0 each update is one complete native segment, despite
            // its TextUpdating kind. GetTextAsync flattens these segment boundaries.
            await foreach (var segment in client.GetStreamingTextAsync(wavStream, options, cancellationToken)
                .ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = segment.Text?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    results.Add(new TranscriptionResult
                    {
                        Text = text,
                        Start = segment.StartTime ??
                            throw new InvalidOperationException("Whisper segment is missing its start time."),
                        End = segment.EndTime ??
                            throw new InvalidOperationException("Whisper segment is missing its end time."),
                        Language = whisperLang
                    });
                }
            }

            // The shipped adapter may break its iterator on cancellation. Never
            // turn that successful enumeration into a successful partial utterance.
            cancellationToken.ThrowIfCancellationRequested();
            return results;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Convert raw 16-bit PCM float samples to a WAV MemoryStream.
    /// Whisper.net processes WAV streams natively.
    /// </summary>
    private static System.IO.MemoryStream PcmToWavStream(float[] samples, int sampleRate)
    {
        var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true);

        int bitsPerSample = 16;
        short channels = 1;
        int byteRate = sampleRate * channels * bitsPerSample / 8;
        short blockAlign = (short)(channels * bitsPerSample / 8);
        int dataSize = samples.Length * blockAlign;

        // RIFF header
        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);

        // fmt subchunk
        writer.Write("fmt "u8);
        writer.Write(16); // subchunk size
        writer.Write((short)1); // PCM format
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write((short)bitsPerSample);

        // data subchunk
        writer.Write("data"u8);
        writer.Write(dataSize);

        // Convert float [-1.0, 1.0] to int16
        foreach (var sample in samples)
        {
            var clamped = Math.Clamp(sample, -1.0f, 1.0f);
            var int16 = (short)(clamped * 32767);
            writer.Write(int16);
        }

        writer.Flush();
        ms.Position = 0;
        return ms;
    }

    /// <summary>
    /// Reduce a BCP-47 tag (e.g. "en-US", "zh-Hans-CN") to the 2-letter
    /// language subtag that Whisper.net's WithLanguage call expects.
    /// "auto" passes through unchanged. Returns "auto" for nulls/whitespace
    /// or values that don't begin with at least 2 ASCII letters.
    /// </summary>
    internal static string NormalizeForWhisper(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return "auto";
        var trimmed = language.Trim();
        if (string.Equals(trimmed, "auto", StringComparison.OrdinalIgnoreCase)) return "auto";

        // Take everything up to the first '-' (the primary subtag) and lowercase.
        var dash = trimmed.IndexOf('-');
        var primary = (dash >= 0 ? trimmed[..dash] : trimmed).ToLowerInvariant();

        // Whisper expects 2-letter ISO 639-1. If the caller handed us a
        // 3-letter ISO 639-3 tag (no good cross-walk without a table) or
        // garbage, fall back to auto-detection rather than silently
        // sending an invalid value.
        if (primary.Length != 2 || primary[0] is < 'a' or > 'z' || primary[1] is < 'a' or > 'z')
            return "auto";

        return primary;
    }

    private void ReleaseModel()
    {
        var client = _client;
        var factory = _factory;
        _client = null;
        _factory = null;
        _loadedModelPath = null;
        Exception? clientFailure = null;
        try
        {
            client?.Dispose();
        }
        catch (Exception error)
        {
            clientFailure = error;
            throw;
        }
        finally
        {
            // Before first transcription the lazy adapter does not own the eager
            // factory yet. Afterward WhisperFactory.Dispose is idempotent (1.9.0).
            try
            {
                factory?.Dispose();
            }
            catch (Exception cleanupError) when (clientFailure != null)
            {
                _logger.Error("Whisper model cleanup failed after client disposal failure.", cleanupError);
            }
        }
    }

    private void EnterQuiescentLifecycle()
    {
        if (!_gate.Wait(0))
            throw new InvalidOperationException("Whisper is busy. Use the asynchronous model lifecycle API.");
    }

    /// <summary>Dispose when idle. Use DisposeAsync if transcription may be active.</summary>
    public void Dispose()
    {
        EnterQuiescentLifecycle();
        try
        {
            DisposeCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        try
        {
            DisposeCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    private void DisposeCore()
    {
        if (_disposed) return;
        _disposed = true;
        ReleaseModel();
        // Keep the managed gate alive so already queued callers can observe
        // disposal rather than race a disposed semaphore.
    }
}

#pragma warning restore MEAI001
