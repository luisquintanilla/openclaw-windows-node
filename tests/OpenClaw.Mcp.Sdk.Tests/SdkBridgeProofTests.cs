using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OpenClaw.Shared;
using OpenClaw.Shared.Mcp;
using OpenClaw.Shared.Telemetry;
using Xunit.Abstractions;

namespace OpenClaw.Mcp.Sdk.Tests;

public sealed class SdkBridgeProofTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("2024-11-05")]
    [InlineData("2025-03-26")]
    [InlineData("2025-06-18")]
    [InlineData("2025-11-25")]
    public async Task OfficialClient_NegotiatesAndCallsLiveRegistry(string version)
    {
        var caps = new List<INodeCapability> { new FakeCapability() };
        await using var session = new Session(caps);
        await using var client = await session.ConnectAsync(version);
        Assert.Equal(version, client.NegotiatedProtocolVersion);

        var tools = await client.ListToolsAsync(cancellationToken: session.Token);
        Assert.Equal(["proof.echo", "proof.fail", "proof.throw", "proof.wait"], tools.Select(t => t.Name));
        Assert.Equal("object", tools[0].JsonSchema.GetProperty("type").GetString());
        caps.Add(new FakeCapability("late"));
        tools = await client.ListToolsAsync(cancellationToken: session.Token);
        Assert.Equal("late.echo", tools[4].Name);

        var result = await client.CallToolAsync("late.echo",
            new Dictionary<string, object?> { ["value"] = 42 }, cancellationToken: session.Token);
        Assert.False(result.IsError);
        Assert.Null(result.StructuredContent);
        Assert.Equal("""{"value":42}""", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
    }

    [Theory]
    [InlineData("2024-11-05", "2024-11-05")]
    [InlineData("2025-11-25", "2025-11-25")]
    [InlineData("2099-01-01", null)]
    public async Task RawInitialize_UsesExplicitExpectedNegotiation(string requested, string? expected)
    {
        await using var session = new Session([], output: output.WriteLine);
        await session.WriteAsync(Initialize(requested));
        var response = await session.ReadAsync();
        Assert.Equal(1, response.GetProperty("id").GetInt32());
        Assert.Equal("2.0", response.GetProperty("jsonrpc").GetString());
        if (expected is null)
        {
            var error = response.GetProperty("error");
            Assert.Equal(-32022, error.GetProperty("code").GetInt32());
            Assert.Equal(requested, error.GetProperty("data").GetProperty("requested").GetString());
            Assert.Equal(["2024-11-05", "2025-03-26", "2025-06-18", "2025-11-25"],
                error.GetProperty("data").GetProperty("supported").EnumerateArray().Select(v => v.GetString()));
            Assert.False(response.TryGetProperty("result", out _));
            return;
        }
        var result = response.GetProperty("result");
        Assert.Equal(expected, result.GetProperty("protocolVersion").GetString());
        Assert.Equal("openclaw-sdk-proof", result.GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.False(result.GetProperty("capabilities").GetProperty("tools").GetProperty("listChanged").GetBoolean());
    }

    [Fact]
    public async Task RawToolTranscript_PreservesArgumentsTextAndToolErrors()
    {
        var fake = new FakeCapability();
        await using var session = new Session([fake], output: output.WriteLine);
        await session.InitializeAsync();
        await session.WriteAsync("""{"jsonrpc":"2.0","id":"call-a","method":"tools/call","params":{"name":"proof.echo","arguments":{"value":42}}}""");
        var response = await session.ReadAsync();
        Assert.Equal("call-a", response.GetProperty("id").GetString());
        Assert.True(JsonElement.DeepEquals(
            Parse("""{"content":[{"type":"text","text":"{\"value\":42}"}],"isError":false}"""),
            response.GetProperty("result")));
        Assert.Equal(42, fake.LastRequest!.Args.GetProperty("value").GetInt32());

        await session.WriteAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"proof.fail"}}""");
        response = await session.ReadAsync();
        Assert.True(JsonElement.DeepEquals(
            Parse("""{"content":[{"type":"text","text":"approval denied"}],"isError":true}"""),
            response.GetProperty("result")));
        Assert.Equal(2, fake.Calls);
    }

    [Fact]
    public async Task CapabilityException_IsSanitizedAndCompletesDiagnostics()
    {
        await using var session = new Session([new FakeCapability()]);
        await session.InitializeAsync();
        await session.WriteAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"proof.throw"}}""");
        var response = await session.ReadAsync();
        Assert.Equal(-32603, response.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal("internal error", response.GetProperty("error").GetProperty("message").GetString());
        Assert.DoesNotContain("private-fixture-detail", response.GetRawText());
        var completion = await session.Completion.Task.WaitAsync(session.Token);
        Assert.Equal(NodeToolErrorCategory.CapabilityFailure, completion.ErrorCategory);
    }

    [Theory]
    [InlineData(NodeToolErrorCategory.PermissionDenied)]
    [InlineData(NodeToolErrorCategory.ExecPolicyDenied)]
    [InlineData(NodeToolErrorCategory.SandboxDenied)]
    public async Task CapabilityDenial_PreservesExecutionDiagnostic(NodeToolErrorCategory category)
    {
        var fake = new FakeCapability { Denial = new(category, NodeToolExecutionMode.Sandbox) };
        await using var session = new Session([fake]);
        await using var client = await session.ConnectAsync("2025-11-25");
        var result = await client.CallToolAsync("proof.fail", cancellationToken: session.Token);
        Assert.True(result.IsError);
        Assert.Equal("approval denied", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        var completion = await session.Completion.Task.WaitAsync(session.Token);
        Assert.Equal(category, completion.ErrorCategory);
        Assert.Equal(NodeToolExecutionMode.Sandbox, completion.ExecutionMode);
        Assert.Equal(NodeToolOutcome.Failure, completion.Outcome);
    }

    [Fact]
    public async Task OfficialClientCancellation_StopsCapability()
    {
        var fake = new FakeCapability();
        await using var session = new Session([fake]);
        await using var client = await session.ConnectAsync("2025-11-25");
        using var cancellation = new CancellationTokenSource();
        var call = client.CallToolAsync("proof.wait", cancellationToken: cancellation.Token);
        await fake.Entered.Task.WaitAsync(session.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await call);
        await fake.Exited.Task.WaitAsync(session.Token);
        Assert.Equal(NodeToolOutcome.Canceled, (await session.Completion.Task.WaitAsync(session.Token)).Outcome);
    }

    [Fact]
    public async Task UnknownTool_RetainsLegacyToolErrorConvention()
    {
        var fake = new FakeCapability();
        await using var session = new Session([fake]);
        await session.InitializeAsync();
        await session.WriteAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"unknown"}}""");
        var result = (await session.ReadAsync()).GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Equal("Unknown tool: unknown", result.GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task ListTools_RetainsRegistryOrderAndSinglePage()
    {
        await using var session = new Session([new FakeCapability("z"), new FakeCapability("a")]);
        await session.InitializeAsync();
        await session.WriteAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{"cursor":"unused"}}""");
        var result = (await session.ReadAsync()).GetProperty("result");
        Assert.False(result.TryGetProperty("nextCursor", out _));
        Assert.Equal(["z.echo", "z.fail", "z.throw", "z.wait", "a.echo", "a.fail", "a.throw", "a.wait"],
            result.GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString()));
        Assert.Equal("z capability: z.echo", result.GetProperty("tools")[0].GetProperty("description").GetString());
    }

    [Fact]
    public async Task Shutdown_CancelsInFlightCapability()
    {
        var fake = new FakeCapability();
        await using var session = new Session([fake]);
        await session.InitializeAsync();
        await session.WriteAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"proof.wait"}}""");
        await fake.Entered.Task.WaitAsync(session.Token);
        await session.StopAsync();
        await fake.Exited.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, fake.Calls);
    }

    [Theory]
    [InlineData("unknown/method")]
    [InlineData("resources/list")]
    [InlineData("prompts/list")]
    public async Task UnsupportedMethod_IsSdkProtocolError(string method)
    {
        await using var session = new Session([]);
        await session.InitializeAsync();
        await session.WriteAsync($$"""{"jsonrpc":"2.0","id":8,"method":"{{method}}"}""");
        var response = await session.ReadAsync();
        Assert.Equal(8, response.GetProperty("id").GetInt32());
        Assert.Equal(-32601, response.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Notifications_DoNotRespondOrInvokeTools()
    {
        var fake = new FakeCapability();
        await using var session = new Session([fake]);
        await session.InitializeAsync();
        await session.WriteAsync("""{"jsonrpc":"2.0","method":"unknown/notification"}""");
        await session.WriteAsync("""{"jsonrpc":"2.0","method":"tools/call","params":{"name":"proof.echo"}}""");
        await session.WriteAsync("""{"jsonrpc":"2.0","id":99,"method":"ping"}""");
        var response = await session.ReadAsync();
        Assert.Equal(99, response.GetProperty("id").GetInt32());
        Assert.Equal("{}", response.GetProperty("result").GetRawText());
        Assert.Equal(0, fake.Calls);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"1\"")]
    [InlineData("9223372036854775807")]
    public async Task RequestIds_RoundTripWithoutStringNumericConflation(string id)
    {
        await using var session = new Session([]);
        await session.InitializeAsync();
        await session.WriteAsync($$"""{"jsonrpc":"2.0","id":{{id}},"method":"ping"}""");
        Assert.Equal(id, (await session.ReadAsync()).GetProperty("id").GetRawText());
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("""{"jsonrpc":"2.0","id":1.5,"method":"ping"}""")]
    [InlineData("""{"jsonrpc":"2.0","id":9223372036854775808,"method":"ping"}""")]
    public async Task UncorrelatableMalformedStreamInput_IsDroppedBeforeNextPing(string invalid)
    {
        await using var session = new Session([]);
        await session.InitializeAsync();
        await session.WriteAsync(invalid);
        await session.WriteAsync("""{"jsonrpc":"2.0","id":99,"method":"ping"}""");
        Assert.Equal(99, (await session.ReadAsync()).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Deadline_ReachesCapabilityAndPreservesTimeoutToolError()
    {
        var fake = new FakeCapability();
        await using var session = new Session([fake], TimeSpan.FromMilliseconds(100));
        await session.InitializeAsync();
        await session.WriteAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"proof.wait"}}""");
        var result = (await session.ReadAsync()).GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Equal("request timed out", result.GetProperty("content")[0].GetProperty("text").GetString());
        await fake.Exited.Task.WaitAsync(session.Token);
        var completion = await session.Completion.Task.WaitAsync(session.Token);
        Assert.Equal(NodeToolErrorCategory.Timeout, completion.ErrorCategory);
    }

    [Fact]
    public async Task CancellationNotification_ReachesExistingPolicyAndCapability()
    {
        var fake = new FakeCapability();
        await using var session = new Session([fake]);
        await session.InitializeAsync();
        await session.WriteAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"proof.wait"}}""");
        await fake.Entered.Task.WaitAsync(session.Token);
        await session.WriteAsync("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":2}}""");
        await fake.Exited.Task.WaitAsync(session.Token);
        var completion = await session.Completion.Task.WaitAsync(session.Token);
        Assert.Equal(NodeToolOutcome.Canceled, completion.Outcome);
        Assert.Equal(NodeToolErrorCategory.Other, completion.ErrorCategory);
        await session.WriteAsync("""{"jsonrpc":"2.0","id":99,"method":"ping"}""");
        Assert.Equal(99, (await session.ReadAsync()).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task CancellationAfterHandlerReturn_CompletesDeliveryBeforeShutdown()
    {
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var filters = new McpServerFilters();
        filters.Request.CallToolFilters.Add(next => async (request, token) =>
        {
            var result = await next(request, token);
            returned.TrySetResult();
            // Deliberately hold a completed handler before the SDK attempts response delivery.
            await resume.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return result;
        });
        await using var session = new Session([new FakeCapability()], filters: filters);
        try
        {
            await session.InitializeAsync();
            await session.WriteAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"proof.echo"}}""");
            await returned.Task.WaitAsync(session.Token);
            Assert.Equal(1, session.PendingDeliveryCount);
            Assert.Equal(0, session.CompletionCount);

            await session.WriteAsync("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":2}}""");
            var completion = await session.Completion.Task.WaitAsync(session.Token);
            Assert.Equal(0, session.PendingDeliveryCount);
            Assert.Equal(1, session.CompletionCount);
            Assert.Equal(NodeToolErrorCategory.TransportFailure, completion.ErrorCategory);
            Assert.Equal(NodeToolOutcome.Failure, completion.Outcome);

            resume.TrySetResult();
            await session.WriteAsync("""{"jsonrpc":"2.0","id":99,"method":"ping"}""");
            Assert.Equal(99, (await session.ReadAsync()).GetProperty("id").GetInt32());
            await session.StopAsync();
            Assert.Equal(1, session.CompletionCount);
        }
        finally
        {
            resume.TrySetResult();
        }
    }

    [Fact]
    public async Task DisposeFailure_CompletesPendingDeliveryExactlyOnce()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var completions = new List<NodeToolTelemetryCompletion>();
        var bridge = new McpToolBridge(() => [new FakeCapability()]);
        bridge.ToolTelemetryCompleted += (_, completion) => completions.Add(completion);
        var adapter = new SdkBridgeProof(new LifecycleTransport(failDispose: true), bridge, TimeSpan.FromSeconds(5));
        await using var server = adapter.CreateServer();
        var request = new RequestContext<CallToolRequestParams>(server,
            new JsonRpcRequest { Id = new RequestId(2), Method = "tools/call" },
            new CallToolRequestParams { Name = "proof.echo" });
        await server.ServerOptions.Handlers.CallToolHandler!(request, cancellation.Token);
        Assert.Equal(1, adapter.PendingDeliveryCount);
        Assert.Empty(completions);

        var error = await Assert.ThrowsAsync<IOException>(async () => await adapter.DisposeAsync());
        Assert.Equal("fixture dispose failure", error.Message);
        Assert.Equal(0, adapter.PendingDeliveryCount);
        Assert.Equal(NodeToolErrorCategory.TransportFailure, Assert.Single(completions).ErrorCategory);
        await cancellation.CancelAsync();
        Assert.Single(completions);
    }

    [Fact]
    public async Task LateResponse_DoesNotClaimReusedIdDelivery()
    {
        using var firstCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var secondCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var completions = new List<NodeToolTelemetryCompletion>();
        var bridge = new McpToolBridge(() => [new FakeCapability()]);
        bridge.ToolTelemetryCompleted += (_, completion) => completions.Add(completion);
        var transport = new LifecycleTransport();
        await using var adapter = new SdkBridgeProof(transport, bridge, TimeSpan.FromSeconds(5));
        await using var server = adapter.CreateServer();
        var first = new RequestContext<CallToolRequestParams>(server,
            new JsonRpcRequest { Id = new RequestId(2), Method = "tools/call" },
            new CallToolRequestParams { Name = "proof.echo" });
        await server.ServerOptions.Handlers.CallToolHandler!(first, firstCancellation.Token);
        await firstCancellation.CancelAsync();
        Assert.Equal(NodeToolErrorCategory.TransportFailure, Assert.Single(completions).ErrorCategory);

        var second = new RequestContext<CallToolRequestParams>(server,
            new JsonRpcRequest { Id = new RequestId(2), Method = "tools/call" },
            new CallToolRequestParams { Name = "proof.echo" });
        await server.ServerOptions.Handlers.CallToolHandler!(second, secondCancellation.Token);
        Assert.Equal(1, adapter.PendingDeliveryCount);
        await first.JsonRpcRequest.Context!.RelatedTransport!.SendMessageAsync(
            new JsonRpcResponse { Id = new RequestId(2), Result = new JsonObject() });
        Assert.Equal(1, adapter.PendingDeliveryCount);
        Assert.Single(completions);

        await second.JsonRpcRequest.Context!.RelatedTransport!.SendMessageAsync(
            new JsonRpcResponse { Id = new RequestId(2), Result = new JsonObject() }, secondCancellation.Token);
        Assert.Equal(0, adapter.PendingDeliveryCount);
        Assert.Equal(2, completions.Count);
        Assert.Equal(NodeToolOutcome.Success, completions[1].Outcome);
        Assert.Equal(2, transport.SentCount);
        await secondCancellation.CancelAsync();
        Assert.Equal(2, completions.Count);
    }

    [Fact]
    public async Task Baseline_CharacterizesNonstandardInputsWithoutChangingThem()
    {
        var fake = new FakeCapability();
        var bridge = new McpToolBridge(() => [fake]);
        var initialized = Parse((await bridge.HandleRequestAsync(Initialize("2025-11-25")))!);
        Assert.Equal("2024-11-05", initialized.GetProperty("result").GetProperty("protocolVersion").GetString());
        var fractional = Parse((await bridge.HandleRequestAsync("""{"jsonrpc":"2.0","id":1.5,"method":"ping"}"""))!);
        Assert.Equal("1.5", fractional.GetProperty("id").GetRawText());
        var malformed = Parse((await bridge.HandleRequestAsync("{broken"))!);
        Assert.Equal(-32700, malformed.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Null(await bridge.HandleRequestAsync("""{"jsonrpc":"2.0","method":"tools/call","params":{"name":"proof.echo"}}"""));
        Assert.Equal(1, fake.Calls);
        var resources = Parse((await bridge.HandleRequestAsync("""{"jsonrpc":"2.0","id":2,"method":"resources/list"}"""))!);
        Assert.Equal("[]", resources.GetProperty("result").GetProperty("resources").GetRawText());
    }

    private static string Initialize(string version) =>
        $$$$"""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"{{{{version}}}}","capabilities":{},"clientInfo":{"name":"raw-proof","version":"1"}}}""";

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class Session : IAsyncDisposable
    {
        private readonly CancellationTokenSource _lifetime = new(TimeSpan.FromSeconds(15));
        private readonly Pipe _toServer = new();
        private readonly Pipe _toClient = new();
        private readonly McpServer _server;
        private readonly SdkBridgeProof _adapter;
        private readonly Task _run;
        private StreamWriter? _writer;
        private StreamReader? _reader;
        private readonly Action<string>? _output;
        private int _completionCount;
        public TaskCompletionSource<NodeToolTelemetryCompletion> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token => _lifetime.Token;
        public int PendingDeliveryCount => _adapter.PendingDeliveryCount;
        public int CompletionCount => Volatile.Read(ref _completionCount);

        public Session(
            IReadOnlyList<INodeCapability> caps,
            TimeSpan? deadline = null,
            Action<string>? output = null,
            McpServerFilters? filters = null)
        {
            _output = output;
            var bridge = new McpToolBridge(() => caps);
            bridge.ToolTelemetryCompleted += (_, completion) =>
            {
                Interlocked.Increment(ref _completionCount);
                Completion.TrySetResult(completion);
            };
            var transport = new StreamServerTransport(_toServer.Reader.AsStream(), _toClient.Writer.AsStream());
            _adapter = new SdkBridgeProof(transport, bridge, deadline ?? TimeSpan.FromSeconds(5));
            _server = _adapter.CreateServer(filters);
            _run = _server.RunAsync(Token);
        }

        public Task<McpClient> ConnectAsync(string version) =>
            McpClient.CreateAsync(
                new StreamClientTransport(_toServer.Writer.AsStream(), _toClient.Reader.AsStream()),
                new McpClientOptions
                {
                    ProtocolVersion = version,
                    ClientInfo = new() { Name = "official-proof", Version = "1" },
                    InitializationTimeout = TimeSpan.FromSeconds(5),
                },
                cancellationToken: Token);

        public async Task InitializeAsync()
        {
            await WriteAsync(Initialize("2024-11-05"));
            Assert.Equal("2024-11-05", (await ReadAsync()).GetProperty("result").GetProperty("protocolVersion").GetString());
            await WriteAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        }

        public Task WriteAsync(string line)
        {
            _output?.Invoke($"C -> S: {line}");
            _writer ??= new StreamWriter(_toServer.Writer.AsStream(), new UTF8Encoding(false), leaveOpen: true)
            {
                AutoFlush = true,
            };
            return _writer.WriteLineAsync(line.AsMemory(), Token);
        }

        public async Task<JsonElement> ReadAsync()
        {
            _reader ??= new StreamReader(_toClient.Reader.AsStream(), leaveOpen: true);
            var line = await _reader.ReadLineAsync(Token).AsTask().WaitAsync(TimeSpan.FromSeconds(5), Token)
                ?? throw new IOException("SDK stream ended before response.");
            _output?.Invoke($"S -> C: {line}");
            return Parse(line);
        }

        public async Task StopAsync()
        {
            await _lifetime.CancelAsync();
            try { await _run.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        }

        public async ValueTask DisposeAsync()
        {
            await _lifetime.CancelAsync();
            try
            {
                await _server.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                try { await _run.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            }
            finally
            {
                if (_writer != null)
                    await _writer.DisposeAsync();
                _reader?.Dispose();
                await _adapter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                await _toServer.Writer.CompleteAsync();
                await _toClient.Reader.CompleteAsync();
                _lifetime.Dispose();
            }
        }
    }

    private sealed class LifecycleTransport(bool failDispose = false) : ITransport
    {
        private readonly Channel<JsonRpcMessage> _messages = Channel.CreateBounded<JsonRpcMessage>(1);
        public string? SessionId => null;
        public ChannelReader<JsonRpcMessage> MessageReader => _messages.Reader;
        public int SentCount { get; private set; }
        public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SentCount++;
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync()
        {
            _messages.Writer.TryComplete();
            return failDispose
                ? ValueTask.FromException(new IOException("fixture dispose failure"))
                : ValueTask.CompletedTask;
        }
    }

    private sealed class FakeCapability(string category = "proof") : INodeCapability
    {
        private int _calls;
        public string Category => category;
        public IReadOnlyList<string> Commands => [$"{category}.echo", $"{category}.fail", $"{category}.throw", $"{category}.wait"];
        public int Calls => Volatile.Read(ref _calls);
        public NodeInvokeRequest? LastRequest { get; private set; }
        public NodeToolDiagnostic? Denial { get; init; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CanHandle(string command) => Commands.Contains(command);
        public Task<NodeInvokeResponse> ExecuteAsync(NodeInvokeRequest request) => ExecuteAsync(request, CancellationToken.None);

        public async Task<NodeInvokeResponse> ExecuteAsync(NodeInvokeRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            LastRequest = request;
            if (request.Command.EndsWith(".fail", StringComparison.Ordinal))
                return new() { Ok = false, Error = "approval denied", Diagnostic = Denial };
            if (request.Command.EndsWith(".throw", StringComparison.Ordinal))
                throw new InvalidOperationException("private-fixture-detail");
            if (request.Command.EndsWith(".wait", StringComparison.Ordinal))
            {
                Entered.TrySetResult();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                finally { Exited.TrySetResult(); }
            }
            return new() { Ok = true, Payload = new { value = 42 } };
        }
    }
}
