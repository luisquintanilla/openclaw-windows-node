using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using OpenClaw.Shared.Audio;
using OpenClaw.Shared.Capabilities;
using OpenClaw.TestSupport;

#pragma warning disable MEAI001 // Exercise the pinned experimental speech boundary.

namespace OpenClaw.Shared.Tests;

public sealed class SpeechToTextServiceTests
{
    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("zh-Hans-CN", "zh")]
    [InlineData("AUTO", "auto")]
    [InlineData("abc", "auto")]
    public async Task Transcribe_PreservesPcmBytesOptionsAndOrderedSegments(string language, string expectedLanguage)
    {
        using var fixture = new Fixture();
        fixture.Client.Updates =
        [
            Update("  second  ", 700, 1100),
            Update(" \t ", 1100, 1200),
            Update(" first\n", 200, 650)
        ];
        float[] samples = [-2, -1, -0.5f, 0, 0.5f, 1, 2];

        var result = await fixture.Service.TranscribeAsync(samples, language);

        Assert.Equal(Convert.FromHexString(
            "524946463200000057415645666D74201000000001000100803E0000007D000002001000646174610E0000000180018001C00000FF3FFF7FFF7F"),
            fixture.Client.WavBytes);
        Assert.Equal(new float[] { -2, -1, -0.5f, 0, 0.5f, 1, 2 }, samples);
        Assert.Equal(expectedLanguage, fixture.Client.Options!.SpeechLanguage);
        Assert.Equal(16000, fixture.Client.Options.SpeechSampleRate);
        Assert.Null(fixture.Client.Options.TextLanguage);
        Assert.Null(fixture.Client.Options.ModelId);
        Assert.Null(fixture.Client.Options.RawRepresentationFactory);
        var property = Assert.Single(fixture.Client.Options.AdditionalProperties!);
        Assert.Equal("Threads", property.Key);
        Assert.Equal(Math.Max(1, Environment.ProcessorCount / 2), Assert.IsType<int>(property.Value));
        Assert.Collection(result,
            segment => AssertSegment(segment, "second", 700, 1100, expectedLanguage),
            segment => AssertSegment(segment, "first", 200, 650, expectedLanguage));
        Assert.Equal(0, fixture.Client.GetServiceCalls);
        Assert.False(fixture.Client.Stream!.CanRead);
    }

    [Fact]
    public async Task Transcribe_DoesNotEnterAdapterOnCallerSynchronizationContext()
    {
        using var fixture = new Fixture();
        var previous = SynchronizationContext.Current;
        Task<List<TranscriptionResult>> operation;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new CallerContext());
            operation = fixture.Service.TranscribeAsync([0]);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
        await operation;
        Assert.Null(fixture.Client.ContextAtInvocation);
    }

    [Fact]
    public async Task Transcribe_EmptyAudioAndBlankSegmentsRemainEmpty()
    {
        using var fixture = new Fixture();
        fixture.Client.Updates = [Update("", 0, 0), Update(" \r\n ", 0, 20)];
        Assert.Empty(await fixture.Service.TranscribeAsync([]));
        Assert.Equal(44, fixture.Client.WavBytes!.Length);
        Assert.Equal(0, BitConverter.ToInt32(fixture.Client.WavBytes, 40));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Transcribe_RejectsMissingSegmentTimes(bool missingStart)
    {
        using var fixture = new Fixture();
        var update = Update("text", 0, 100);
        if (missingStart) update.StartTime = null;
        else update.EndTime = null;
        fixture.Client.Updates = [update];
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.TranscribeAsync([0]));
        Assert.Equal(missingStart ? "Whisper segment is missing its start time." :
            "Whisper segment is missing its end time.", error.Message);
    }

    [Fact]
    public async Task Transcribe_PreCanceledTokenDoesNotInvokeClient()
    {
        using var fixture = new Fixture();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Service.TranscribeAsync([0], cancellationToken: cts.Token));
        Assert.Equal(0, fixture.Client.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transcribe_CanceledIteratorCannotReturnPartialSuccess(bool emitAfterCancel)
    {
        using var fixture = new Fixture();
        using var cts = new CancellationTokenSource();
        fixture.Client.Updates = [Update("partial", 0, 100), Update("late", 100, 200)];
        fixture.Client.AfterFirst = () =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        };
        fixture.Client.StopAfterFirst = !emitAfterCancel;

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Service.TranscribeAsync([0], cancellationToken: cts.Token));
        Assert.Equal(cts.Token, error.CancellationToken);
        Assert.False(fixture.Client.Stream!.CanRead);
        fixture.Client.AfterFirst = null;
        fixture.Client.StopAfterFirst = false;
        Assert.Equal(2, (await fixture.Service.TranscribeAsync([0])).Count);
    }

    [Fact]
    public async Task Transcribe_ProviderFailureIsNotPartialSuccessAndCapabilitySanitizesIt()
    {
        using var fixture = new Fixture();
        var failure = new InvalidOperationException("private provider detail");
        fixture.Client.Updates = [Update("partial", 0, 100)];
        fixture.Client.AfterFirst = () => Task.FromException(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.TranscribeAsync([0])));

        var capability = new SttCapability(NullLogger.Instance);
        capability.ListenRequested += async (_, token) =>
        {
            await fixture.Service.TranscribeAsync([0], cancellationToken: token);
            throw new InvalidOperationException("A failed provider must not reach this line.");
        };
        var response = await capability.ExecuteAsync(new NodeInvokeRequest { Command = "stt.listen" });
        Assert.False(response.Ok);
        Assert.Equal("Listen failed", response.Error);
    }

    [Fact]
    public async Task Transcribe_CancellationRemainsCapabilityCancellation()
    {
        using var fixture = new Fixture();
        using var cts = new CancellationTokenSource();
        fixture.Client.Updates = [Update("partial", 0, 100)];
        fixture.Client.AfterFirst = () =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        };
        var capability = new SttCapability(NullLogger.Instance);
        capability.ListenRequested += async (_, token) =>
        {
            await fixture.Service.TranscribeAsync([0], cancellationToken: token);
            return new SttListenResult { Text = "must not escape" };
        };
        var response = await capability.ExecuteAsync(new NodeInvokeRequest { Command = "stt.listen" }, cts.Token);
        Assert.False(response.Ok);
        Assert.Equal("Listen canceled", response.Error);
    }

    [Fact]
    public async Task Transcribe_SerializesCallsAndCancelsQueuedCallWithoutReplacingClient()
    {
        using var fixture = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Client.Updates = [Update("segment", 0, 100)];
        fixture.Client.AfterFirst = async () => { entered.TrySetResult(); await release.Task; };

        var first = fixture.Service.TranscribeAsync([0]);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = fixture.Service.TranscribeAsync([0]);
        using var cts = new CancellationTokenSource();
        var canceled = fixture.Service.TranscribeAsync([0], cancellationToken: cts.Token);
        try
        {
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
            Assert.Equal(1, fixture.Client.Calls);
            Assert.False(second.IsCompleted);
            Assert.Equal(0, fixture.Client.DisposeCalls);
        }
        finally
        {
            release.TrySetResult();
        }
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(2, fixture.Client.Calls);
        Assert.Equal(1, fixture.Client.MaxActiveCalls);
        Assert.Equal(1, fixture.LoadCalls);
    }

    [Theory]
    [InlineData("unload")]
    [InlineData("reload")]
    [InlineData("dispose")]
    public async Task ModelLifetime_AsyncUiCallerRemainsResponsiveUntilEnumerationEnds(string action)
    {
        using var fixture = new Fixture();
        var original = fixture.Client;
        var originalModel = fixture.Model;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        original.Updates = [Update("segment", 0, 100)];
        original.AfterFirst = async () => { entered.TrySetResult(); await release.Task; };
        var transcription = fixture.Service.TranscribeAsync([0]);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var previous = SynchronizationContext.Current;
        Task mutation;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new CallerContext());
            mutation = action switch
            {
                "unload" => fixture.Service.UnloadModelAsync(),
                "reload" => fixture.Service.LoadModelAsync(fixture.ModelPath),
                _ => fixture.Service.DisposeAsync().AsTask()
            };
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
        try
        {
            Assert.False(mutation.IsCompleted);
            Assert.Equal(0, original.DisposeCalls);
            Assert.Equal(0, originalModel.DisposeCalls);
        }
        finally
        {
            release.TrySetResult();
        }
        await Task.WhenAll(transcription, mutation).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, original.DisposeCalls);
        Assert.Equal(1, originalModel.DisposeCalls);
        Assert.Equal(0, original.ActiveCallsAtDisposal);
        Assert.Null(original.ContextAtDisposal);
        Assert.Equal(action == "reload", fixture.Service.IsModelLoaded);
        Assert.Equal(action == "reload" ? fixture.ModelPath : null, fixture.Service.LoadedModelPath);
        if (action == "reload")
        {
            Assert.NotSame(original, fixture.Client);
            Assert.Empty(await fixture.Service.TranscribeAsync([0]));
            Assert.Equal(1, fixture.Client.Calls);
        }
    }

    [Theory]
    [InlineData("load")]
    [InlineData("unload")]
    [InlineData("dispose")]
    public async Task ModelLifetime_SynchronousBusyCallFailsWithoutChangingOwnership(string action)
    {
        using var fixture = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Client.Updates = [Update("segment", 0, 100)];
        fixture.Client.AfterFirst = async () => { entered.TrySetResult(); await release.Task; };
        var transcription = fixture.Service.TranscribeAsync([0]);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
            {
                if (action == "load") fixture.Service.LoadModel(fixture.ModelPath);
                else if (action == "unload") fixture.Service.UnloadModel();
                else fixture.Service.Dispose();
            });
            Assert.Equal("Whisper is busy. Use the asynchronous model lifecycle API.", error.Message);
            Assert.True(fixture.Service.IsModelLoaded);
            Assert.Equal(fixture.ModelPath, fixture.Service.LoadedModelPath);
            Assert.Equal(0, fixture.Client.DisposeCalls);
            Assert.Equal(0, fixture.Model.DisposeCalls);
            Assert.Equal(1, fixture.LoadCalls);
        }
        finally
        {
            release.TrySetResult();
        }
        Assert.Single(await transcription);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ModelLifetime_QueuedCancellationLeavesCurrentModelOwned(bool reload)
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Client.Updates = [Update("segment", 0, 100)];
        fixture.Client.AfterFirst = async () => { entered.TrySetResult(); await release.Task; };
        var transcription = fixture.Service.TranscribeAsync([0]);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            var mutation = reload
                ? fixture.Service.LoadModelAsync(fixture.ModelPath, cancellation.Token)
                : fixture.Service.UnloadModelAsync(cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mutation);
            Assert.True(fixture.Service.IsModelLoaded);
            Assert.Equal(fixture.ModelPath, fixture.Service.LoadedModelPath);
            Assert.Equal(0, fixture.Client.DisposeCalls);
            Assert.Equal(0, fixture.Model.DisposeCalls);
            Assert.Equal(1, fixture.LoadCalls);
        }
        finally
        {
            release.TrySetResult();
        }
        Assert.Single(await transcription);
    }

    [Fact]
    public async Task ModelLifetime_AsyncLoadAndRepeatedDisposalStayOffCallerContext()
    {
        using var directory = new TempDirectory();
        var path = directory.Combine("model.bin");
        File.WriteAllBytes(path, [1]);
        SynchronizationContext? loadContext = new();
        var client = new FakeClient();
        await using var service = new SpeechToTextService(NullLogger.Instance, _ =>
        {
            loadContext = SynchronizationContext.Current;
            return (new FakeModel(), () => client);
        });
        var previous = SynchronizationContext.Current;
        Task load;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new CallerContext());
            load = service.LoadModelAsync(path);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
        await load;
        Assert.Null(loadContext);
        Assert.True(service.IsModelLoaded);
        await Task.WhenAll(service.DisposeAsync().AsTask(), service.DisposeAsync().AsTask());
        Assert.False(service.IsModelLoaded);
        Assert.Equal(1, client.DisposeCalls);
        Assert.Null(client.ContextAtDisposal);
    }

    [Fact]
    public async Task ModelLifetime_ReadinessTracksExplicitLoadFailureUnloadAndDispose()
    {
        using var directory = new TempDirectory();
        var path = directory.Combine("model.bin");
        File.WriteAllBytes(path, [1]);
        var client = new FakeClient();
        var fail = false;
        using var service = new SpeechToTextService(NullLogger.Instance,
            _ => fail ? throw new InvalidOperationException("load failed") : (new FakeModel(), () => client));
        Assert.False(service.IsModelLoaded);
        Assert.Null(service.LoadedModelPath);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TranscribeAsync([0]));
        service.LoadModel(path);
        Assert.True(service.IsModelLoaded);
        Assert.Equal(path, service.LoadedModelPath);
        Assert.Throws<FileNotFoundException>(() => service.LoadModel(directory.Combine("missing.bin")));
        Assert.True(service.IsModelLoaded);
        Assert.Equal(0, client.DisposeCalls);
        fail = true;
        Assert.Throws<InvalidOperationException>(() => service.LoadModel(path));
        Assert.Equal(1, client.DisposeCalls);
        Assert.False(service.IsModelLoaded);
        Assert.Null(service.LoadedModelPath);
        service.UnloadModel();
        service.Dispose();
        service.Dispose();
        Assert.Equal(1, client.DisposeCalls);
        Assert.Throws<ObjectDisposedException>(() => service.LoadModel(path));
        Assert.Throws<ObjectDisposedException>(() => service.UnloadModel());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.TranscribeAsync([0]));
    }

    [Fact]
    public void ModelLifetime_DisposesUnusedLoadedClientExactlyOnce()
    {
        using var fixture = new Fixture();
        var client = fixture.Client;
        fixture.Service.UnloadModel();
        fixture.Service.Dispose();
        Assert.Equal(0, client.Calls);
        Assert.Equal(1, client.DisposeCalls);
        Assert.Equal(1, fixture.Model.DisposeCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ModelLifetime_ModelCleanupFollowsClientEvenIfClientDisposalThrows(bool failDisposal)
    {
        using var fixture = new Fixture();
        var order = new List<string>();
        fixture.Client.OnDispose = () =>
        {
            order.Add("client");
            if (failDisposal) throw new InvalidOperationException("client disposal failed");
        };
        fixture.Model.OnDispose = () => order.Add("model");
        if (failDisposal)
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.UnloadModelAsync());
            Assert.Equal("client disposal failed", error.Message);
        }
        else
        {
            await fixture.Service.UnloadModelAsync();
        }
        Assert.Equal(new[] { "client", "model" }, order);
        Assert.Equal(1, fixture.Model.DisposeCalls);
        Assert.False(fixture.Service.IsModelLoaded);
        Assert.Null(fixture.Service.LoadedModelPath);
        await fixture.Service.DisposeAsync();
        Assert.Equal(1, fixture.Client.DisposeCalls);
        Assert.Equal(1, fixture.Model.DisposeCalls);
    }

    private static SpeechToTextResponseUpdate Update(string text, int startMs, int endMs) => new(text)
    {
        Kind = SpeechToTextResponseUpdateKind.TextUpdating,
        StartTime = TimeSpan.FromMilliseconds(startMs),
        EndTime = TimeSpan.FromMilliseconds(endMs)
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ModelLifetime_ClientCreationFailureCleansOwnedModelAndPreservesPrimaryError(bool cleanupFails)
    {
        using var directory = new TempDirectory();
        var path = directory.Combine("model.bin");
        File.WriteAllBytes(path, [1]);
        var model = new FakeModel();
        var failure = new InvalidOperationException("client creation failed");
        if (cleanupFails) model.OnDispose = () => throw new IOException("cleanup failed");
        await using var service = new SpeechToTextService(NullLogger.Instance, _ =>
            (model, () => throw failure));
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => service.LoadModelAsync(path)));
        Assert.Equal(1, model.DisposeCalls);
        Assert.False(service.IsModelLoaded);
        Assert.Null(service.LoadedModelPath);
        await service.DisposeAsync();
        Assert.Equal(1, model.DisposeCalls);
    }

    [Fact]
    public async Task ModelLifetime_TwoDisposalFailuresPreserveClientFailure()
    {
        using var fixture = new Fixture();
        var failure = new InvalidOperationException("client disposal failed");
        fixture.Client.OnDispose = () => throw failure;
        fixture.Model.OnDispose = () => throw new IOException("model disposal failed");
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.UnloadModelAsync()));
        Assert.Equal(1, fixture.Client.DisposeCalls);
        Assert.Equal(1, fixture.Model.DisposeCalls);
        Assert.False(fixture.Service.IsModelLoaded);
        Assert.Null(fixture.Service.LoadedModelPath);
    }

    private static void AssertSegment(TranscriptionResult segment, string text, int startMs, int endMs, string language)
    {
        Assert.Equal(text, segment.Text);
        Assert.Equal(TimeSpan.FromMilliseconds(startMs), segment.Start);
        Assert.Equal(TimeSpan.FromMilliseconds(endMs), segment.End);
        Assert.Equal(language, segment.Language);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TempDirectory _directory = new();
        public string ModelPath { get; }
        public SpeechToTextService Service { get; }
        public FakeClient Client { get; private set; } = null!;
        public FakeModel Model { get; private set; } = null!;
        public int LoadCalls { get; private set; }

        public Fixture()
        {
            ModelPath = _directory.Combine("model.bin");
            File.WriteAllBytes(ModelPath, [1]);
            Service = new SpeechToTextService(NullLogger.Instance, _ =>
            {
                LoadCalls++;
                Model = new FakeModel();
                return (Model, () => Client = new FakeClient());
            });
            Service.LoadModel(ModelPath);
        }

        public void Dispose()
        {
            Service.Dispose();
            _directory.Dispose();
        }
    }

    private sealed class CallerContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) =>
            throw new InvalidOperationException("Native lifecycle must not post back to the caller context.");
    }

    private sealed class FakeModel : IDisposable
    {
        public int DisposeCalls { get; private set; }
        public Action? OnDispose { get; set; }

        public void Dispose()
        {
            DisposeCalls++;
            OnDispose?.Invoke();
        }
    }

    private sealed class FakeClient : ISpeechToTextClient
    {
        public SpeechToTextResponseUpdate[] Updates { get; set; } = [];
        public Func<Task>? AfterFirst { get; set; }
        public bool StopAfterFirst { get; set; }
        public SpeechToTextOptions? Options { get; private set; }
        public Stream? Stream { get; private set; }
        public byte[]? WavBytes { get; private set; }
        public int Calls { get; private set; }
        public int DisposeCalls { get; private set; }
        public int GetServiceCalls { get; private set; }
        public int MaxActiveCalls { get; private set; }
        public int ActiveCallsAtDisposal { get; private set; }
        public SynchronizationContext? ContextAtInvocation { get; private set; }
        public SynchronizationContext? ContextAtDisposal { get; private set; }
        public Action? OnDispose { get; set; }
        private int _activeCalls;

        public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(
            Stream audioSpeechStream, SpeechToTextOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Assert.Equal(0, DisposeCalls);
            ContextAtInvocation = SynchronizationContext.Current;
            Calls++;
            MaxActiveCalls = Math.Max(MaxActiveCalls, ++_activeCalls);
            Options = options;
            Stream = audioSpeechStream;
            try
            {
                using var copy = new MemoryStream();
                await audioSpeechStream.CopyToAsync(copy, cancellationToken);
                WavBytes = copy.ToArray();
                for (var index = 0; index < Updates.Length; index++)
                {
                    yield return Updates[index];
                    if (index == 0 && AfterFirst != null) await AfterFirst();
                    if (StopAfterFirst) yield break;
                }
            }
            finally
            {
                _activeCalls--;
            }
        }

        public Task<SpeechToTextResponse> GetTextAsync(Stream audioSpeechStream,
            SpeechToTextOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The service must consume segments, not flattened text.");

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            GetServiceCalls++;
            throw new NotSupportedException("Metadata is not available from the pinned adapter.");
        }

        public void Dispose()
        {
            DisposeCalls++;
            ContextAtDisposal = SynchronizationContext.Current;
            ActiveCallsAtDisposal = _activeCalls;
            OnDispose?.Invoke();
        }
    }
}

#pragma warning restore MEAI001
