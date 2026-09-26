using OpenClaw.Shared.Capabilities;

namespace OpenClaw.Shared.Mcp;

/// <summary>
/// Discovery metadata only. Capability parsers remain authoritative, including
/// their legacy fallback behavior for optional arguments of the wrong JSON type.
/// </summary>
internal static class McpAudioToolSchemas
{
    private static readonly object Transcribe = new
    {
        type = "object",
        additionalProperties = true,
        properties = new
        {
            maxDurationMs = new
            {
                type = "integer",
                minimum = 1,
                maximum = SttCapability.MaxTranscribeDurationMs,
                description = "Required capture duration in milliseconds. Send an Int32 JSON integer, not a string, fraction, decimal or exponent notation.",
            },
            language = new
            {
                description = "Optional language string: auto (case-insensitive), or a 2-3 letter language with optional 4-letter script and 2-letter or 3-digit region, separated by hyphens. Trimmed before validation. Omitted, blank, null or non-string values use the configured SttLanguage setting.",
                examples = new[] { "en-US", "auto" },
            },
        },
        required = new[] { "maxDurationMs" },
    };

    private static readonly object Listen = new
    {
        type = "object",
        additionalProperties = true,
        properties = new
        {
            timeoutMs = new
            {
                description = $"Optional Int32 JSON integer in milliseconds. Clamped to {SttCapability.MinListenTimeoutMs}..{SttCapability.MaxListenTimeoutMs}, not rejected outside that range. Omitted, null, non-number, fractional, decimal/exponent notation or out-of-Int32 values use {SttCapability.DefaultListenTimeoutMs}.",
                @default = SttCapability.DefaultListenTimeoutMs,
                examples = new[] { SttCapability.DefaultListenTimeoutMs },
            },
            language = new
            {
                description = "Optional language string: auto (case-insensitive), or a 2-3 letter language with optional 4-letter script and 2-letter or 3-digit region, separated by hyphens. Trimmed before validation. Omitted, blank, null or non-string values use auto.",
                @default = SttCapability.AutoLanguage,
                examples = new[] { "auto", "en-US" },
            },
        },
    };

    private static readonly object Speak = new
    {
        type = "object",
        additionalProperties = true,
        properties = new
        {
            text = new
            {
                type = "string",
                minLength = 1,
                description = $"Required text to speak. Trimmed before validation; must be nonblank and at most {TtsCapability.MaxTextLength} UTF-16 code units after trimming.",
            },
            provider = new
            {
                description = "Optional provider string: piper, windows, elevenlabs or minimax (trimmed, case-insensitive). Omitted, blank, null or non-string values use the configured provider, with Windows fallback when unavailable. Explicit nonblank provider requests are strict and never silently rerouted. Availability depends on local settings and installed voices.",
                examples = new[] { "piper", "windows", "elevenlabs", "minimax" },
            },
            voiceId = new
            {
                description = "Optional voice ID string, trimmed; overrides the selected provider's configured voice. Omitted, blank, null or non-string values use the configured voice. Not universally required: readiness depends on provider and settings.",
            },
            model = new
            {
                description = "Optional model string for cloud providers, trimmed. Omitted, blank, null or non-string values use the configured model. Ignored by local providers.",
            },
            interrupt = new
            {
                description = "Optional boolean. Only JSON true interrupts in-progress playback; omitted or any other value is false.",
                @default = false,
                examples = new[] { false, true },
            },
        },
        required = new[] { "text" },
    };

    private static readonly object Status = new
    {
        type = "object",
        additionalProperties = true,
        properties = new { },
        description = "No arguments are consumed. Unknown properties are ignored.",
    };

    internal static object? ForCommand(string command) => command switch
    {
        SttCapability.TranscribeCommand => Transcribe,
        SttCapability.ListenCommand => Listen,
        SttCapability.StatusCommand or TtsCapability.StatusCommand => Status,
        TtsCapability.SpeakCommand => Speak,
        _ => null,
    };
}
