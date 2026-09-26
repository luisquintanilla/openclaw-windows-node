using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OpenClaw.Shared.Mcp;

namespace OpenClaw.Mcp.Sdk.Tests;

// Deliberately test-only: this is a protocol-core experiment, not an HTTP host.
internal sealed class SdkBridgeProof(ITransport inner, McpToolBridge bridge, TimeSpan deadline) : ITransport
{
    private readonly ConcurrentDictionary<RequestId, PendingDelivery> _deliveries = new();

    public string? SessionId => inner.SessionId;
    public ChannelReader<JsonRpcMessage> MessageReader => inner.MessageReader;
    internal int PendingDeliveryCount => _deliveries.Count;

    public McpServer CreateServer(McpServerFilters? filters = null) => McpServer.Create(this, new McpServerOptions
    {
        ServerInfo = new() { Name = "openclaw-sdk-proof", Version = "1.0.0" },
        Capabilities = new() { Tools = new() { ListChanged = false } },
        ScopeRequests = false,
        Filters = filters ?? new(),
        Handlers = new()
        {
            ListToolsHandler = (_, _) => ValueTask.FromResult(
                JsonSerializer.SerializeToElement(bridge.HandleToolsList())
                    .Deserialize<ListToolsResult>(McpJsonUtilities.DefaultOptions)!),
            CallToolHandler = CallAsync,
        },
    });

    private async ValueTask<CallToolResult> CallAsync(
        RequestContext<CallToolRequestParams> request, CancellationToken callerCancellation)
    {
        using var timeout = new CancellationTokenSource(deadline);
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, callerCancellation);
        // Use the SDK-parsed params, not a second JSON-RPC envelope or the legacy method switch.
        var result = await bridge.InvokeToolAsync(
            JsonSerializer.SerializeToElement(request.Params, McpJsonUtilities.DefaultOptions),
            requestKey: null,
            execution.Token,
            callerCancellationToken: callerCancellation);
        if (callerCancellation.IsCancellationRequested)
        {
            // SDK cancellation suppresses the response. There is no delivery to await.
            result.PendingTelemetry.CompleteDelivery();
            callerCancellation.ThrowIfCancellationRequested();
        }
        var id = request.JsonRpcRequest.Id;
        var context = request.JsonRpcRequest.Context ??= new();
        var delivery = new PendingDelivery(this, id, context.RelatedTransport ?? inner, result.PendingTelemetry);
        if (!_deliveries.TryAdd(id, delivery))
        {
            result.PendingTelemetry.CompleteDelivery(typeof(InvalidOperationException));
            throw new McpProtocolException("duplicate active request id", McpErrorCode.InvalidRequest);
        }
        // The SDK preserves RelatedTransport for both successful and error responses.
        // Bind delivery to this invocation, not just its potentially reused request ID.
        context.RelatedTransport = delivery;
        delivery.ObserveCancellation(callerCancellation);
        if (result.IsProtocolError)
            throw new McpProtocolException("internal error", McpErrorCode.InternalError);

        return JsonSerializer.SerializeToElement(result.Result)
            .Deserialize<CallToolResult>(McpJsonUtilities.DefaultOptions)!;
    }

    public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default) =>
        inner.SendMessageAsync(message, cancellationToken);

    private bool TryTake(RequestId id, PendingDelivery delivery) =>
        ((ICollection<KeyValuePair<RequestId, PendingDelivery>>)_deliveries).Remove(new(id, delivery));

    public async ValueTask DisposeAsync()
    {
        try
        {
            await inner.DisposeAsync();
        }
        finally
        {
            foreach (var id in _deliveries.Keys)
            {
                if (_deliveries.TryRemove(id, out var delivery))
                    delivery.Complete(typeof(OperationCanceledException));
            }
        }
    }

    private sealed class PendingDelivery(
        SdkBridgeProof owner,
        RequestId id,
        ITransport transport,
        McpToolBridge.McpPendingToolTelemetry telemetry) : ITransport
    {
        private readonly object _lock = new();
        private CancellationTokenRegistration _registration;
        private bool _completed;

        public string? SessionId => transport.SessionId;
        public ChannelReader<JsonRpcMessage> MessageReader =>
            throw new NotSupportedException("This transport only completes a related response.");

        public void ObserveCancellation(CancellationToken token)
        {
            var registration = token.UnsafeRegister(static state => ((PendingDelivery)state!).Cancel(), this);
            lock (_lock)
            {
                if (!_completed)
                {
                    _registration = registration;
                    return;
                }
            }
            // Registration can invoke synchronously when cancellation already won.
            registration.Unregister();
        }

        private void Cancel()
        {
            // The SDK can cancel after our handler returns but before calling the transport.
            if (owner.TryTake(id, this))
                Complete(typeof(OperationCanceledException));
        }

        public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
        {
            var ownsDelivery = (message is JsonRpcResponse or JsonRpcError) && owner.TryTake(id, this);
            try
            {
                await transport.SendMessageAsync(message, cancellationToken);
                if (ownsDelivery)
                    Complete();
            }
            catch (Exception ex)
            {
                if (ownsDelivery)
                    Complete(ex.GetType());
                throw;
            }
        }

        public ValueTask DisposeAsync()
        {
            Cancel();
            return ValueTask.CompletedTask;
        }

        public void Complete(Type? deliveryError = null)
        {
            CancellationTokenRegistration registration;
            lock (_lock)
            {
                if (_completed)
                    return;
                _completed = true;
                registration = _registration;
            }
            // Do not wait for a concurrent cancellation callback that lost ownership.
            registration.Unregister();
            telemetry.CompleteDelivery(deliveryError);
        }
    }
}
