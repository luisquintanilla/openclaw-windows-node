using OpenClaw.TestSupport;

namespace OpenClaw.Shared.Tests;

public sealed class SpeechToTextLifecycleContractTests
{
    [Fact]
    public void VoiceService_AwaitsModelLoadAndDisposal()
    {
        // Retirement: replace this source guard when VoiceService has a WinUI-free
        // runtime seam that can execute both lifecycle paths under controlled inference.
        var path = Path.Combine(ProductionSourceFiles.FindRepoRoot(),
            "src", "OpenClaw.Tray.WinUI", "Services", "VoiceService.cs");
        var source = File.ReadAllText(path);
        Assert.Contains("await _stt.LoadModelAsync(modelPath, cancellationToken)", source);
        Assert.Contains("await _stt.DisposeAsync()", source);
        Assert.DoesNotContain("_stt.LoadModel(", source);
        Assert.DoesNotContain("_stt.UnloadModel(", source);
        Assert.DoesNotContain("_stt.Dispose(", source);
    }
}
