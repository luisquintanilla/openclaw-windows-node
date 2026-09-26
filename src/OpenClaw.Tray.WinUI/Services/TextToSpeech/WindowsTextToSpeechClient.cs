using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using OpenClaw.Shared.Capabilities;

#pragma warning disable MEAI001 // Experimental speech API, pinned to Microsoft.Extensions.AI.Abstractions 10.9.0.

namespace OpenClawTray.Services;

/// <summary>
/// Adapts Windows synthesis, not playback. The callback owns a fresh synthesizer
/// and returns an owned, complete WAV per call. It must support concurrent calls.
/// Disposing this adapter rejects new calls but does not abort an in-flight OS
/// operation; its caller's cancellation token controls that operation.
/// </summary>
internal sealed class WindowsTextToSpeechClient(
    Func<string, string?, CancellationToken, Task<byte[]>> synthesize) : ITextToSpeechClient
{
    private readonly Func<string, string?, CancellationToken, Task<byte[]>> _synthesize =
        synthesize ?? throw new ArgumentNullException(nameof(synthesize));
    private readonly TextToSpeechClientMetadata _metadata = new(providerName: TtsCapability.WindowsProvider);
    private int _disposed;

    public async Task<TextToSpeechResponse> GetAudioAsync(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        ValidateOptions(options);
        cancellationToken.ThrowIfCancellationRequested();
        var wav = await _synthesize(text, options?.VoiceId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateWav(wav);
        return new TextToSpeechResponse([new DataContent(wav, "audio/wav")]);
    }

    /// <summary>Yields one completed WAV, not incremental or low-latency audio.</summary>
    public async IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(
        string text,
        TextToSpeechOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetAudioAsync(text, options, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        yield return response.ToTextToSpeechResponseUpdates()[0];
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null)
            return null;
        if (serviceType == typeof(TextToSpeechClientMetadata))
            return _metadata;
        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);

    internal static void ValidateWav(ReadOnlySpan<byte> wav)
    {
        if (wav.Length < 12 || !wav[..4].SequenceEqual("RIFF"u8) || !wav.Slice(8, 4).SequenceEqual("WAVE"u8))
            throw new InvalidOperationException("Windows TTS did not return WAV audio.");
    }

    private static void ValidateOptions(TextToSpeechOptions? options)
    {
        if (options is null)
            return;

        if (options.AudioFormat is not null
            && !string.Equals(options.AudioFormat, "wav", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(options.AudioFormat, "audio/wav", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Windows TTS supports WAV output only.");

        // This pilot exposes the existing voice-only Windows path. Do not
        // silently accept richer MEAI controls which that path does not apply.
        if (options.ModelId is not null || options.Language is not null
            || options.Speed is not null || options.Pitch is not null || options.Volume is not null
            || options.RawRepresentationFactory is not null || options.AdditionalProperties is { Count: > 0 })
            throw new NotSupportedException("Windows TTS supports only VoiceId and AudioFormat options.");
    }
}
