using System.Text.Json;
using OpenClaw.Shared.Capabilities;
using OpenClaw.Shared.Mcp;
using Xunit.Abstractions;

namespace OpenClaw.Shared.Tests;

public class McpAudioToolSchemaTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ToolsList_ExportsActualAudioCatalog()
    {
        var harness = new AudioHarness();
        var response = await harness.Bridge.HandleRequestAsync(
            """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");
        output.WriteLine(response!);
        using var document = JsonDocument.Parse(response!);
        var tools = document.RootElement.GetProperty("result").GetProperty("tools");
        Assert.Equal(
            ["stt.transcribe", "stt.listen", "stt.status", "tts.speak", "tts.status"],
            tools.EnumerateArray().Select(tool => tool.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task ToolsCall_ExportsRepresentativeAudioResponses()
    {
        var harness = new AudioHarness();
        foreach (var (command, arguments, succeeds) in new[]
        {
            ("stt.transcribe", """{"maxDurationMs":1,"unknown":true}""", true),
            ("stt.transcribe", """{"maxDurationMs":30001}""", false),
            ("stt.listen", """{"timeoutMs":0,"language":false}""", true),
            ("stt.status", """{"unknown":true}""", true),
            ("tts.speak", """{"text":" hello ","provider":" PiPeR ","interrupt":"true"}""", true),
            ("tts.speak", """{"text":" "}""", false),
            ("tts.status", """{"unknown":true}""", true),
        })
        {
            var result = await CallAsync(harness, command, arguments, output.WriteLine);
            Assert.Equal(!succeeds, result.GetProperty("isError").GetBoolean());
        }
        Assert.Equal(5, harness.Calls);
    }

    [Theory]
    [InlineData("stt.transcribe", "maxDurationMs,language", "maxDurationMs")]
    [InlineData("stt.listen", "timeoutMs,language", "")]
    [InlineData("stt.status", "", "")]
    [InlineData("tts.speak", "text,provider,voiceId,model,interrupt", "text")]
    [InlineData("tts.status", "", "")]
    public async Task ToolsList_AudioSchema_DeclaresPropertiesAndRequiredFields(
        string name, string properties, string required)
    {
        var schema = await GetSchemaAsync(new AudioHarness(), name);
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.True(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(Split(properties),
            schema.GetProperty("properties").EnumerateObject().Select(property => property.Name));
        Assert.Equal(Split(required), schema.TryGetProperty("required", out var values)
            ? values.EnumerateArray().Select(value => value.GetString())
            : []);
    }

    [Fact]
    public async Task ToolsList_AudioSchema_ConstrainsOnlyUnambiguousRawInput()
    {
        var harness = new AudioHarness();
        var transcribe = (await GetSchemaAsync(harness, "stt.transcribe")).GetProperty("properties");
        var duration = transcribe.GetProperty("maxDurationMs");
        Assert.Equal("integer", duration.GetProperty("type").GetString());
        Assert.Equal(1, duration.GetProperty("minimum").GetInt32());
        Assert.Equal(30000, duration.GetProperty("maximum").GetInt32());
        Assert.False(duration.TryGetProperty("default", out _));
        Assert.False(transcribe.GetProperty("language").TryGetProperty("default", out _));

        var listen = (await GetSchemaAsync(harness, "stt.listen")).GetProperty("properties");
        Assert.Equal(30000, listen.GetProperty("timeoutMs").GetProperty("default").GetInt32());
        Assert.Equal("auto", listen.GetProperty("language").GetProperty("default").GetString());

        var speak = (await GetSchemaAsync(harness, "tts.speak")).GetProperty("properties");
        Assert.Equal("string", speak.GetProperty("text").GetProperty("type").GetString());
        Assert.Equal(1, speak.GetProperty("text").GetProperty("minLength").GetInt32());
        Assert.False(speak.GetProperty("text").TryGetProperty("maxLength", out _));
        Assert.Contains("5000 UTF-16", speak.GetProperty("text").GetProperty("description").GetString());
        Assert.False(speak.GetProperty("interrupt").GetProperty("default").GetBoolean());
        Assert.False(speak.GetProperty("provider").TryGetProperty("default", out _));
        Assert.Equal(["piper", "windows", "elevenlabs", "minimax"],
            speak.GetProperty("provider").GetProperty("examples").EnumerateArray()
                .Select(value => value.GetString()));

        // These optional parsers accept every JSON kind (some values select defaults).
        foreach (var property in new[]
        {
            transcribe.GetProperty("language"), listen.GetProperty("timeoutMs"),
            listen.GetProperty("language"), speak.GetProperty("provider"),
            speak.GetProperty("voiceId"), speak.GetProperty("model"), speak.GetProperty("interrupt"),
        })
        {
            Assert.All(property.EnumerateObject(), keyword =>
                Assert.Contains(keyword.Name, new[] { "description", "default", "examples" }));
            Assert.False(string.IsNullOrWhiteSpace(property.GetProperty("description").GetString()));
        }
    }

    [Fact]
    public async Task ToolsList_LiveRegistry_RemovesDisabledAudioAndPreservesOtherSchemas()
    {
        var harness = new AudioHarness();
        harness.Capabilities.Clear();
        harness.Capabilities.Add(new UnrelatedCapability());
        var original = await ListToolsAsync(harness);
        var tool = Assert.Single(original.EnumerateArray());
        Assert.Equal("fixture.echo", tool.GetProperty("name").GetString());
        Assert.Equal("fixture capability: fixture.echo", tool.GetProperty("description").GetString());
        Assert.Equal("""{"type":"object","additionalProperties":true,"properties":{}}""",
            tool.GetProperty("inputSchema").GetRawText());

        harness.Capabilities.Add(harness.Stt);
        Assert.Equal(4, (await ListToolsAsync(harness)).GetArrayLength());
        harness.Capabilities.Remove(harness.Stt);
        harness.Capabilities.Add(harness.Tts);
        Assert.Equal(["fixture.echo", "tts.speak", "tts.status"],
            (await ListToolsAsync(harness)).EnumerateArray()
                .Select(value => value.GetProperty("name").GetString()));
        harness.Capabilities.Remove(harness.Tts);
        Assert.Equal(original.GetRawText(), (await ListToolsAsync(harness)).GetRawText());
        Assert.Equal("Unknown tool: tts.speak",
            ErrorText(await CallAsync(harness, "tts.speak", """{"text":"hello"}""")));
        Assert.Equal(0, harness.Calls);
    }

    [Theory]
    [InlineData("{}", "Missing required maxDurationMs")]
    [InlineData("""{"durationMs":5000}""", "Missing required maxDurationMs")]
    [InlineData("""{"MaxDurationMs":5000}""", "Missing required maxDurationMs")]
    [InlineData("""{"maxDurationMs":0}""", "Missing required maxDurationMs")]
    [InlineData("""{"maxDurationMs":-1}""", "Missing required maxDurationMs")]
    [InlineData("""{"maxDurationMs":30001}""", "maxDurationMs exceeds 30000 ms")]
    [InlineData("""{"maxDurationMs":"5000"}""", "Missing required maxDurationMs")]
    [InlineData("""{"maxDurationMs":5000.0}""", "Missing required maxDurationMs")]
    [InlineData("""{"maxDurationMs":5e3}""", "Missing required maxDurationMs")]
    [InlineData("""{"maxDurationMs":1.5}""", "Missing required maxDurationMs")]
    [InlineData("""{"maxDurationMs":2147483648}""", "Missing required maxDurationMs")]
    [InlineData("""{"maxDurationMs":null}""", "Missing required maxDurationMs")]
    [InlineData("""{"maxDurationMs":true}""", "Missing required maxDurationMs")]
    [InlineData("""{"maxDurationMs":[]}""", "Missing required maxDurationMs")]
    [InlineData("""{"maxDurationMs":{}}""", "Missing required maxDurationMs")]
    [InlineData("""{"maxDurationMs":1,"language":"en_US"}""", "Invalid language tag")]
    public async Task ToolsCall_Transcribe_RejectsInvalidInputBeforeHandler(string arguments, string error)
    {
        var harness = new AudioHarness();
        Assert.Equal(error, ErrorText(await CallAsync(harness, "stt.transcribe", arguments)));
        Assert.Equal(0, harness.Calls);
    }

    [Theory]
    [InlineData("""{"maxDurationMs":1}""", 1, null)]
    [InlineData("""{"maxDurationMs":30000,"unknown":{"kept":true}}""", 30000, null)]
    [InlineData("""{"maxDurationMs":5000,"language":" AuTo "}""", 5000, "auto")]
    [InlineData("""{"maxDurationMs":5000,"language":" zh-Hant-TW "}""", 5000, "zh-Hant-TW")]
    [InlineData("""{"maxDurationMs":5000,"language":"es-419"}""", 5000, "es-419")]
    [InlineData("""{"maxDurationMs":5000,"language":"  "}""", 5000, null)]
    [InlineData("""{"maxDurationMs":5000,"language":false}""", 5000, null)]
    [InlineData("""{"maxDurationMs":5000,"language":123}""", 5000, null)]
    [InlineData("""{"maxDurationMs":5000,"language":null}""", 5000, null)]
    [InlineData("""{"maxDurationMs":5000,"language":[]}""", 5000, null)]
    [InlineData("""{"maxDurationMs":5000,"language":{}}""", 5000, null)]
    public async Task ToolsCall_Transcribe_PreservesBoundsLanguageFallbackAndResult(
        string arguments, int duration, string? language)
    {
        var harness = new AudioHarness();
        var payload = SuccessPayload(await CallAsync(harness, "stt.transcribe", arguments));
        Assert.Equal(1, harness.Calls);
        Assert.Equal(duration, harness.TranscribeArgs!.MaxDurationMs);
        Assert.Equal(language, harness.TranscribeArgs.Language);
        Assert.Equal("""{"transcribed":true,"text":"fixture transcript","durationMs":25,"language":"en-US","engineEffective":"whisper"}""",
            payload.GetRawText());
    }

    [Theory]
    [InlineData("{}", 30000, "auto")]
    [InlineData("""{"timeoutMs":0}""", 1000, "auto")]
    [InlineData("""{"timeoutMs":-2147483648}""", 1000, "auto")]
    [InlineData("""{"timeoutMs":2147483647}""", 120000, "auto")]
    [InlineData("""{"timeoutMs":120001}""", 120000, "auto")]
    [InlineData("""{"timeoutMs":1000,"language":" EN-us ","ignored":true}""", 1000, "EN-us")]
    [InlineData("""{"timeoutMs":120000,"language":" AUTO "}""", 120000, "auto")]
    [InlineData("""{"timeoutMs":"1000","language":null}""", 30000, "auto")]
    [InlineData("""{"timeoutMs":1000.0,"language":true}""", 30000, "auto")]
    [InlineData("""{"timeoutMs":1e3,"language":1}""", 30000, "auto")]
    [InlineData("""{"timeoutMs":1.5,"language":[]}""", 30000, "auto")]
    [InlineData("""{"timeoutMs":2147483648,"language":{}}""", 30000, "auto")]
    [InlineData("""{"timeoutMs":null,"language":" "}""", 30000, "auto")]
    [InlineData("""{"timeoutMs":false}""", 30000, "auto")]
    [InlineData("""{"timeoutMs":[]}""", 30000, "auto")]
    [InlineData("""{"timeoutMs":{}}""", 30000, "auto")]
    [InlineData("""{"TimeoutMs":1000,"lang":"fr"}""", 30000, "auto")]
    public async Task ToolsCall_Listen_PreservesClampingDefaultsAndResult(
        string arguments, int timeout, string language)
    {
        var harness = new AudioHarness();
        var payload = SuccessPayload(await CallAsync(harness, "stt.listen", arguments));
        Assert.Equal(1, harness.Calls);
        Assert.Equal(timeout, harness.ListenArgs!.TimeoutMs);
        Assert.Equal(language, harness.ListenArgs.Language);
        Assert.Equal("""{"text":"fixture transcript","language":"en-US","durationMs":25,"segments":[{"Text":"fixture transcript","StartMs":0,"EndMs":25}],"engineEffective":"whisper"}""",
            payload.GetRawText());
    }

    [Fact]
    public async Task ToolsCall_Listen_RejectsInvalidLanguageBeforeHandler()
    {
        var harness = new AudioHarness();
        Assert.Equal("Invalid language tag",
            ErrorText(await CallAsync(harness, "stt.listen", """{"language":"english please"}""")));
        Assert.Equal(0, harness.Calls);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"Text":"hello"}""")]
    [InlineData("""{"text":null}""")]
    [InlineData("""{"text":true}""")]
    [InlineData("""{"text":5000}""")]
    [InlineData("""{"text":[]}""")]
    [InlineData("""{"text":{}}""")]
    [InlineData("""{"text":""}""")]
    [InlineData("""{"text":" \t\r\n "}""")]
    public async Task ToolsCall_Speak_RejectsMissingTextBeforeHandler(string arguments)
    {
        var harness = new AudioHarness();
        Assert.Equal("Missing required text", ErrorText(await CallAsync(harness, "tts.speak", arguments)));
        Assert.Equal(0, harness.Calls);
    }

    [Theory]
    [InlineData(5000, true)]
    [InlineData(5001, false)]
    public async Task ToolsCall_Speak_ValidatesTrimmedUtf16Length(int length, bool succeeds)
    {
        var harness = new AudioHarness();
        // A surrogate pair occupies two UTF-16 code units, one JSON Schema character.
        var text = string.Concat(Enumerable.Repeat("\U0001F642", 2500)) + (length == 5001 ? "x" : "");
        var result = await CallAsync(harness, "tts.speak", JsonSerializer.Serialize(new { text = $"  {text}\t" }));
        if (succeeds)
        {
            SuccessPayload(result);
            Assert.Equal(text, harness.SpeakArgs!.Text);
            Assert.Equal(1, harness.Calls);
        }
        else
        {
            Assert.Equal("TTS text exceeds 5000 characters.", ErrorText(result));
            Assert.Equal(0, harness.Calls);
        }
    }

    [Theory]
    [InlineData("{}", null, null, null, false)]
    [InlineData("""{"provider":" PiPeR ","voiceId":" voice ","model":" model ","interrupt":true,"unknown":42}""", "PiPeR", "voice", "model", true)]
    [InlineData("""{"provider":" ","voiceId":"","model":"\t","interrupt":false}""", null, null, null, false)]
    [InlineData("""{"provider":null,"voiceId":false,"model":42,"interrupt":"true"}""", null, null, null, false)]
    [InlineData("""{"provider":true,"voiceId":42,"model":[],"interrupt":1}""", null, null, null, false)]
    [InlineData("""{"provider":42,"voiceId":[],"model":{},"interrupt":null}""", null, null, null, false)]
    [InlineData("""{"provider":[],"voiceId":{},"model":null,"interrupt":[]}""", null, null, null, false)]
    [InlineData("""{"provider":{},"voiceId":null,"model":true,"interrupt":{}}""", null, null, null, false)]
    [InlineData("""{"provider":"future-provider","voice":"not-an-alias","Interrupt":true}""", "future-provider", null, null, false)]
    public async Task ToolsCall_Speak_PreservesOptionalCoercionAndResult(
        string options, string? provider, string? voice, string? model, bool interrupt)
    {
        var harness = new AudioHarness();
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(options)!;
        arguments["text"] = JsonSerializer.SerializeToElement(" hello ");
        var payload = SuccessPayload(await CallAsync(harness, "tts.speak", JsonSerializer.Serialize(arguments)));
        Assert.Equal(1, harness.Calls);
        Assert.Equal("hello", harness.SpeakArgs!.Text);
        Assert.Equal(provider, harness.SpeakArgs.Provider);
        Assert.Equal(voice, harness.SpeakArgs.VoiceId);
        Assert.Equal(model, harness.SpeakArgs.Model);
        Assert.Equal(interrupt, harness.SpeakArgs.Interrupt);
        Assert.Equal("""{"spoken":true,"provider":"windows","requestedProvider":"piper","fellBack":true,"contentType":"audio/wav","durationMs":25}""",
            payload.GetRawText());
    }

    [Theory]
    [InlineData("""{"text":"hello"}""", "piper", "windows", true)]
    [InlineData("""{"text":"hello","provider":" "}""", "piper", "windows", true)]
    [InlineData("""{"text":"hello","provider":" PiPeR "}""", "piper", "piper", false)]
    [InlineData("""{"text":"hello","provider":" ELEVENLABS "}""", "elevenlabs", "elevenlabs", false)]
    [InlineData("""{"text":"hello","provider":"future-provider"}""", "future-provider", "future-provider", false)]
    public async Task ToolsCall_Speak_PreservesExplicitProviderForStrictResolution(
        string arguments, string requested, string effective, bool fellBack)
    {
        var harness = new AudioHarness();
        SuccessPayload(await CallAsync(harness, "tts.speak", arguments));
        var provider = harness.SpeakArgs!.Provider;
        var resolution = TtsCapability.ResolveEffectiveProvider(
            provider, "piper", new HashSet<string> { "windows" }, string.IsNullOrWhiteSpace(provider));
        Assert.Equal(requested, resolution.RequestedProvider);
        Assert.Equal(effective, resolution.EffectiveProvider);
        Assert.Equal(fellBack, resolution.FellBack);
    }

    [Theory]
    [InlineData("stt.status", """{"engine":"whisper","readiness":"ready","modelDownloadProgress":null,"isListenWithVadSupported":true,"isBoundedTranscribeSupported":true}""")]
    [InlineData("tts.status", """{"configuredProvider":"piper","effectiveProvider":"piper","willFallBack":false,"providers":[]}""")]
    public async Task ToolsCall_Status_IgnoresArgumentsAndPreservesResult(string command, string expected)
    {
        var harness = new AudioHarness();
        Assert.Equal(expected,
            SuccessPayload(await CallAsync(harness, command, """{"unknown":[1],"text":false}""")).GetRawText());
        Assert.Equal(1, harness.Calls);
    }

    private static string[] Split(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries);

    private static async Task<JsonElement> ListToolsAsync(AudioHarness harness)
    {
        var response = await harness.Bridge.HandleRequestAsync(
            """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");
        using var document = JsonDocument.Parse(response!);
        return document.RootElement.GetProperty("result").GetProperty("tools").Clone();
    }

    private static async Task<JsonElement> GetSchemaAsync(AudioHarness harness, string name)
        => (await ListToolsAsync(harness)).EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == name).GetProperty("inputSchema");

    private static async Task<JsonElement> CallAsync(
        AudioHarness harness, string command, string arguments, Action<string>? observe = null)
    {
        using var args = JsonDocument.Parse(arguments);
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0", id = 2, method = "tools/call",
            @params = new { name = command, arguments = args.RootElement },
        });
        observe?.Invoke($"REQUEST {request}");
        var response = await harness.Bridge.HandleRequestAsync(request);
        observe?.Invoke($"RESPONSE {response}");
        using var document = JsonDocument.Parse(response!);
        Assert.Equal("2.0", document.RootElement.GetProperty("jsonrpc").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("id").GetInt32());
        Assert.False(document.RootElement.TryGetProperty("error", out _));
        return document.RootElement.GetProperty("result").Clone();
    }

    private static string ContentText(JsonElement result)
    {
        var content = Assert.Single(result.GetProperty("content").EnumerateArray());
        Assert.Equal("text", content.GetProperty("type").GetString());
        return content.GetProperty("text").GetString()!;
    }

    private static string ErrorText(JsonElement result)
    {
        Assert.True(result.GetProperty("isError").GetBoolean());
        return ContentText(result);
    }

    private static JsonElement SuccessPayload(JsonElement result)
    {
        Assert.False(result.GetProperty("isError").GetBoolean());
        using var document = JsonDocument.Parse(ContentText(result));
        return document.RootElement.Clone();
    }

    private sealed class AudioHarness
    {
        public SttCapability Stt { get; } = new(NullLogger.Instance);
        public TtsCapability Tts { get; } = new(NullLogger.Instance);
        public List<INodeCapability> Capabilities { get; }
        public McpToolBridge Bridge { get; }
        public int Calls { get; private set; }
        public SttTranscribeArgs? TranscribeArgs { get; private set; }
        public SttListenArgs? ListenArgs { get; private set; }
        public TtsSpeakArgs? SpeakArgs { get; private set; }

        public AudioHarness()
        {
            Capabilities = [Stt, Tts];
            Bridge = new(() => Capabilities);
            Stt.TranscribeRequested += (args, _) =>
            {
                Calls++;
                TranscribeArgs = args;
                return Task.FromResult(new SttTranscribeResult
                {
                    Transcribed = true, Text = "fixture transcript", DurationMs = 25, Language = "en-US",
                });
            };
            Stt.ListenRequested += (args, _) =>
            {
                Calls++;
                ListenArgs = args;
                return Task.FromResult(new SttListenResult
                {
                    Text = "fixture transcript", Language = "en-US", DurationMs = 25,
                    Segments = [new SttSegment { Text = "fixture transcript", StartMs = 0, EndMs = 25 }],
                });
            };
            Tts.SpeakRequested += (args, _) =>
            {
                Calls++;
                SpeakArgs = args;
                return Task.FromResult(new TtsSpeakResult
                {
                    Provider = "windows", RequestedProvider = "piper", FellBack = true,
                    ContentType = "audio/wav", DurationMs = 25,
                });
            };
            Stt.StatusRequested += _ =>
            {
                Calls++;
                return Task.FromResult(new SttStatusResult
                {
                    Readiness = "ready", IsListenWithVadSupported = true, IsBoundedTranscribeSupported = true,
                });
            };
            Tts.StatusRequested += _ =>
            {
                Calls++;
                return Task.FromResult(new TtsStatusResult());
            };
        }
    }

    private sealed class UnrelatedCapability : NodeCapabilityBase
    {
        public UnrelatedCapability() : base(NullLogger.Instance) { }
        public override string Category => "fixture";
        public override IReadOnlyList<string> Commands => ["fixture.echo"];
        public override Task<NodeInvokeResponse> ExecuteAsync(NodeInvokeRequest request)
            => Task.FromResult(Success());
    }
}
