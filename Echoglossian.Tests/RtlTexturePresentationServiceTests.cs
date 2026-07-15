// <copyright file="RtlTexturePresentationServiceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

using Echoglossian.UIOverlays.TextPresentation;
using Echoglossian.UIOverlays.TranslationOverlay;

using System.Collections.Concurrent;
using System.Numerics;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers asynchronous texture presentation and bounded layout measurements.
/// </summary>
public class RtlTexturePresentationServiceTests
{
    /// <summary>
    /// Ensures an LTR texture request is scheduled once without blocking the
    /// caller and becomes available after the controlled upload completes.
    /// </summary>
    [Theory]
    [InlineData(6, "az")]
    [InlineData(40, "ha")]
    [InlineData(57, "ku")]
    public async Task TryRender_LtrCacheMiss_ReturnsPendingAndSchedulesOnce(
        int languageId,
        string languageCode)
    {
        var creationStarted =
            new TaskCompletionSource<TextureCreationRequest>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var uploadCompletion =
            new TaskCompletionSource<IDalamudTextureWrap>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var creationCount = 0;
        using var service = new RtlTexturePresentationService(
            new Config(),
            (request, _) =>
            {
                Interlocked.Increment(ref creationCount);
                creationStarted.TrySetResult(request);
                return uploadCompletion.Task;
            });
        var request = CreateRequest(
            languageId,
            languageCode,
            "Salam dunya");

        var firstResult = service.TryRender(request);
        var creationRequest = await creationStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5));
        var repeatedResult = service.TryRender(request);

        Assert.Null(firstResult);
        Assert.Null(repeatedResult);
        Assert.False(creationRequest.RightToLeft);
        Assert.Equal(1, Volatile.Read(ref creationCount));

        var texture = new FakeTextureWrap(width: 120, height: 30);
        uploadCompletion.SetResult(texture);
        var completedResult = WaitForRenderedBlock(service, request);

        Assert.False(completedResult.RightAligned);
        completedResult.Texture!.Dispose();
    }

    /// <summary>
    /// Ensures lease disposal after image submission retains an evicted
    /// texture until the next host draw frame begins.
    /// </summary>
    [Fact]
    public async Task BeginDrawFrame_EvictedSubmittedTexture_ReleasesOnNextFrame()
    {
        var starts = new ConcurrentDictionary<string, TaskCompletionSource<bool>>();
        var completions = new ConcurrentDictionary<
            string,
            TaskCompletionSource<IDalamudTextureWrap>>();
        using var service = new RtlTexturePresentationService(
            new Config(),
            (request, _) =>
            {
                starts.GetOrAdd(request.Text, _ => NewSignal()).TrySetResult(true);
                return completions.GetOrAdd(request.Text, _ => NewTextureSignal()).Task;
            },
            textureCacheCapacity: 1);
        var firstRequest = CreateRequest(2, "ar", "first");
        var secondRequest = CreateRequest(2, "ar", "second");
        var firstTexture = new FakeTextureWrap(width: 100, height: 20);
        var secondTexture = new FakeTextureWrap(width: 100, height: 20);

        Assert.Null(service.TryRender(firstRequest));
        await starts.GetOrAdd("first", _ => NewSignal()).Task.WaitAsync(
            TimeSpan.FromSeconds(5));
        completions.GetOrAdd("first", _ => NewTextureSignal()).SetResult(firstTexture);
        service.BeginDrawFrame();
        var firstBlock = WaitForRenderedBlock(service, firstRequest);
        firstBlock.Texture!.Dispose();

        Assert.Null(service.TryRender(secondRequest));
        await starts.GetOrAdd("second", _ => NewSignal()).Task.WaitAsync(
            TimeSpan.FromSeconds(5));
        completions.GetOrAdd("second", _ => NewTextureSignal()).SetResult(secondTexture);
        var secondBlock = WaitForRenderedBlock(service, secondRequest);

        Assert.Equal(0, firstTexture.DisposeCount);

        service.BeginDrawFrame();

        Assert.Equal(1, firstTexture.DisposeCount);
        secondBlock.Texture!.Dispose();
    }

    /// <summary>
    /// Ensures repeated clears share one global worker cap, discard stale
    /// queued generations, and retain the newest request for later execution.
    /// </summary>
    [Fact]
    public async Task Clear_RepeatedWithBlockedUpload_GloballyBoundsRetiredWork()
    {
        var firstCompletion = NewTextureSignal();
        var creationStarted = new ConcurrentQueue<
            TaskCompletionSource<TextureCreationRequest>>();
        var firstStart = NewCreationRequestSignal();
        var currentStart = NewCreationRequestSignal();
        creationStarted.Enqueue(firstStart);
        creationStarted.Enqueue(currentStart);
        using var service = new RtlTexturePresentationService(
            new Config(),
            (request, _) =>
            {
                Assert.True(creationStarted.TryDequeue(out var started));
                started.SetResult(request);
                return request.Text == "blocked"
                    ? firstCompletion.Task
                    : Task.FromResult<IDalamudTextureWrap>(
                        new FakeTextureWrap(width: 20, height: 10));
            },
            pendingTextureCapacity: 2,
            maxConcurrentTextureCreations: 1);

        Assert.Null(service.TryRender(CreateRequest(2, "ar", "blocked")));
        await firstStart.Task.WaitAsync(TimeSpan.FromSeconds(5));

        for (var generation = 0; generation < 5; generation++)
        {
            service.Clear();
            Assert.Null(service.TryRender(
                CreateRequest(2, "ar", $"retired-{generation}")));

            var stats = service.GetDebugStats();
            Assert.Equal(1, stats.ActiveTextureWorkerCount);
            Assert.InRange(stats.QueuedTextureCount, 0, 1);
        }

        service.Clear();
        var currentRequest = CreateRequest(2, "ar", "current");
        Assert.Null(service.TryRender(currentRequest));

        firstCompletion.SetResult(new FakeTextureWrap(width: 10, height: 10));
        var startedCurrentRequest = await currentStart.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.Equal("current", startedCurrentRequest.Text);
        var completedBlock = WaitForRenderedBlock(service, currentRequest);
        completedBlock.Texture!.Dispose();
    }

    /// <summary>
    /// Ensures disposal cancels running work and removes all queued keys even
    /// when a provider operation does not complete.
    /// </summary>
    [Fact]
    public async Task Dispose_BlockedUpload_CancelsAndDropsQueuedState()
    {
        var creationStarted = NewCancellationSignal();
        var blockedCompletion = NewTextureSignal();
        var service = new RtlTexturePresentationService(
            new Config(),
            (_, cancellationToken) =>
            {
                creationStarted.SetResult(cancellationToken);
                return blockedCompletion.Task;
            },
            pendingTextureCapacity: 2,
            maxConcurrentTextureCreations: 1);

        Assert.Null(service.TryRender(CreateRequest(2, "ar", "running")));
        var cancellationToken = await creationStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5));
        Assert.Null(service.TryRender(CreateRequest(2, "ar", "queued")));

        service.Dispose();
        service.Dispose();

        var stats = service.GetDebugStats();
        Assert.True(cancellationToken.IsCancellationRequested);
        Assert.Equal(0, stats.QueuedTextureCount);
        Assert.InRange(stats.ActiveTextureWorkerCount, 0, 1);

        var staleTexture = new FakeTextureWrap(width: 10, height: 10);
        blockedCompletion.SetResult(staleTexture);
        Assert.True(SpinWait.SpinUntil(
            () => staleTexture.DisposeCount == 1,
            TimeSpan.FromSeconds(5)));
    }

    /// <summary>
    /// Ensures clear retires old work atomically and lets the same key start a
    /// fresh generation that stale completion cannot remove.
    /// </summary>
    [Fact]
    public async Task Clear_PendingSameKey_RetiresOldWorkAndStartsFreshCreation()
    {
        var starts = new ConcurrentQueue<
            TaskCompletionSource<CancellationToken>>();
        var completions = new ConcurrentQueue<
            TaskCompletionSource<IDalamudTextureWrap>>();
        var firstStart = NewCancellationSignal();
        var secondStart = NewCancellationSignal();
        var firstCompletion = NewTextureSignal();
        var secondCompletion = NewTextureSignal();
        starts.Enqueue(firstStart);
        starts.Enqueue(secondStart);
        completions.Enqueue(firstCompletion);
        completions.Enqueue(secondCompletion);
        using var service = new RtlTexturePresentationService(
            new Config(),
            (_, cancellationToken) =>
            {
                Assert.True(starts.TryDequeue(out var start));
                Assert.True(completions.TryDequeue(out var completion));
                start.SetResult(cancellationToken);
                return completion.Task;
            },
            maxConcurrentTextureCreations: 1);
        var request = CreateRequest(2, "ar", "same key");

        Assert.Null(service.TryRender(request));
        var firstToken = await firstStart.Task.WaitAsync(TimeSpan.FromSeconds(5));

        service.Clear();
        Assert.Null(service.TryRender(request));

        Assert.True(firstToken.IsCancellationRequested);

        var staleTexture = new FakeTextureWrap(width: 90, height: 20);
        firstCompletion.SetResult(staleTexture);
        await secondStart.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(SpinWait.SpinUntil(
            () => staleTexture.DisposeCount == 1,
            TimeSpan.FromSeconds(5)));
        Assert.Null(service.TryRender(request));

        var currentTexture = new FakeTextureWrap(width: 100, height: 20);
        secondCompletion.SetResult(currentTexture);
        var completedBlock = WaitForRenderedBlock(service, request);

        Assert.Equal(0, currentTexture.DisposeCount);
        completedBlock.Texture!.Dispose();
    }

    /// <summary>
    /// Ensures pending work has fixed admission and concurrency bounds while
    /// rejected requests remain eligible after capacity becomes available.
    /// </summary>
    [Fact]
    public async Task TryRender_UniqueMissBurst_BoundsPendingAndConcurrentWork()
    {
        var starts = new ConcurrentDictionary<string, TaskCompletionSource<bool>>();
        var completions = new ConcurrentDictionary<
            string,
            TaskCompletionSource<IDalamudTextureWrap>>();
        var creationCount = 0;
        using var service = new RtlTexturePresentationService(
            new Config(),
            (request, _) =>
            {
                Interlocked.Increment(ref creationCount);
                starts.GetOrAdd(request.Text, _ => NewSignal()).TrySetResult(true);
                return completions.GetOrAdd(request.Text, _ => NewTextureSignal()).Task;
            },
            pendingTextureCapacity: 2,
            maxConcurrentTextureCreations: 1);
        var firstRequest = CreateRequest(2, "ar", "one");
        var secondRequest = CreateRequest(2, "ar", "two");
        var thirdRequest = CreateRequest(2, "ar", "three");
        var rejectedRequest = CreateRequest(2, "ar", "four");

        Assert.Null(service.TryRender(firstRequest));
        await starts.GetOrAdd("one", _ => NewSignal()).Task.WaitAsync(
            TimeSpan.FromSeconds(5));
        Assert.Null(service.TryRender(secondRequest));
        Assert.Null(service.TryRender(thirdRequest));
        Assert.Equal(2, service.GetDebugStats().PendingTextureCount);
        Assert.Equal(1, Volatile.Read(ref creationCount));

        completions.GetOrAdd("one", _ => NewTextureSignal()).SetResult(
            new FakeTextureWrap(width: 10, height: 10));
        await starts.GetOrAdd("two", _ => NewSignal()).Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.Null(service.TryRender(thirdRequest));
        Assert.Null(service.TryRender(rejectedRequest));
        Assert.Equal(2, service.GetDebugStats().PendingTextureCount);
        Assert.Equal(2, Volatile.Read(ref creationCount));

        completions.GetOrAdd("two", _ => NewTextureSignal()).SetResult(
            new FakeTextureWrap(width: 10, height: 10));
        await starts.GetOrAdd("three", _ => NewSignal()).Task.WaitAsync(
            TimeSpan.FromSeconds(5));
        Assert.Equal(3, Volatile.Read(ref creationCount));

        completions.GetOrAdd("three", _ => NewTextureSignal()).SetResult(
            new FakeTextureWrap(width: 10, height: 10));
        var completedBlock = WaitForRenderedBlock(service, thirdRequest);
        completedBlock.Texture!.Dispose();
    }

    /// <summary>
    /// Ensures one-off failures cannot retain more cooldown keys than the
    /// configured bound.
    /// </summary>
    [Fact]
    public void TryRender_OneOffFailures_BoundsRetryState()
    {
        using var service = new RtlTexturePresentationService(
            new Config(),
            (_, _) => Task.FromException<IDalamudTextureWrap>(
                new InvalidOperationException("expected failure")),
            retryStateCapacity: 2,
            maxConcurrentTextureCreations: 1);

        for (var index = 0; index < 3; index++)
        {
            Assert.Null(service.TryRender(
                CreateRequest(2, "ar", $"failed-{index}")));
            Assert.True(SpinWait.SpinUntil(
                () => service.GetDebugStats().PendingTextureCount == 0,
                TimeSpan.FromSeconds(5)));
        }

        Assert.Equal(2, service.GetDebugStats().RetryStateCount);
    }

    /// <summary>
    /// Ensures the cache key uses the exact font bits and final raster colors
    /// from the same resolved request supplied to texture creation.
    /// </summary>
    [Fact]
    public void TryRender_CloseRasterInputs_DoNotAliasCacheKeys()
    {
        var creationRequests = new ConcurrentQueue<TextureCreationRequest>();
        using var service = new RtlTexturePresentationService(
            new Config(),
            (request, _) =>
            {
                creationRequests.Enqueue(request);
                return Task.FromResult<IDalamudTextureWrap>(
                    new FakeTextureWrap(width: 10, height: 10));
            });
        var firstRequest = CreateRequest(
            2,
            "ar",
            "exact inputs",
            fontScale: 1.00001f,
            textColor: new Vector4(0.4999f, 1f, 1f, 1f));
        var secondRequest = CreateRequest(
            2,
            "ar",
            "exact inputs",
            fontScale: 1.00002f,
            textColor: new Vector4(0.5001f, 1f, 1f, 1f));

        var firstBlock = WaitForRenderedBlock(service, firstRequest);
        firstBlock.Texture!.Dispose();
        var secondBlock = WaitForRenderedBlock(service, secondRequest);
        secondBlock.Texture!.Dispose();

        Assert.Equal(2, creationRequests.Count);
        Assert.True(creationRequests.TryDequeue(out var firstCreation));
        Assert.True(creationRequests.TryDequeue(out var secondCreation));
        Assert.NotEqual(firstCreation.FontSize, secondCreation.FontSize);
        Assert.NotEqual(firstCreation.TextColor.ToArgb(), secondCreation.TextColor.ToArgb());
    }

    /// <summary>
    /// Ensures unique adaptive-width measurements evict older entries instead
    /// of retaining unbounded dialogue text and viewport keys.
    /// </summary>
    [Fact]
    public void ResolveAdaptiveHoverTooltipMaxWidth_WhenCapacityExceeded_EvictsEntry()
    {
        using var service = new RtlTexturePresentationService(
            new Config(),
            (_, _) => Task.FromResult<IDalamudTextureWrap>(
                new FakeTextureWrap(width: 1, height: 1)),
            adaptiveWidthCacheCapacity: 2);

        service.ResolveAdaptiveHoverTooltipMaxWidth(
            CreateRequest(2, "ar", new string('\u0627', 730)),
            viewportWidth: 1280f);
        service.ResolveAdaptiveHoverTooltipMaxWidth(
            CreateRequest(2, "ar", new string('\u0628', 731)),
            viewportWidth: 1280f);
        service.ResolveAdaptiveHoverTooltipMaxWidth(
            CreateRequest(2, "ar", new string('\u062a', 732)),
            viewportWidth: 1280f);

        Assert.Equal(2, service.GetDebugStats().AdaptiveWidthCount);
    }

    /// <summary>
    /// Ensures close viewport inputs remain distinct because the width policy
    /// receives their exact values.
    /// </summary>
    [Fact]
    public void ResolveAdaptiveHoverTooltipMaxWidth_CloseViewports_DoNotAliasKeys()
    {
        using var service = new RtlTexturePresentationService(
            new Config(),
            (_, _) => Task.FromResult<IDalamudTextureWrap>(
                new FakeTextureWrap(width: 1, height: 1)));
        var request = CreateRequest(2, "ar", "short text");

        var firstWidth = service.ResolveAdaptiveHoverTooltipMaxWidth(
            request,
            viewportWidth: 1000.1f);
        var secondWidth = service.ResolveAdaptiveHoverTooltipMaxWidth(
            request,
            viewportWidth: 1000.2f);

        Assert.NotEqual(firstWidth, secondWidth);
        Assert.Equal(2, service.GetDebugStats().AdaptiveWidthCount);
    }

    /// <summary>
    /// Ensures a viewport-derived hover tooltip width cannot exceed the raster
    /// dimension used by texture generation.
    /// </summary>
    [Fact]
    public void ResolveAdaptiveHoverTooltipMaxWidth_ViewportExceedsRasterLimit_ClampsWidth()
    {
        using var service = new RtlTexturePresentationService(
            new Config { HoverTooltipMaxWidth = 10000f },
            (_, _) => Task.FromResult<IDalamudTextureWrap>(
                new FakeTextureWrap(width: 1, height: 1)));

        var maxWidth = service.ResolveAdaptiveHoverTooltipMaxWidth(
            CreateRequest(2, "ar", "tooltip"),
            viewportWidth: 10000f);

        Assert.InRange(maxWidth, 1f, 2048f);
    }

    /// <summary>
    /// Ensures an over-height raster layout is rejected before the injected
    /// upload factory runs and enters the existing retry cooldown.
    /// </summary>
    [Fact]
    public void TryRender_RasterLayoutExceedsHeightLimit_SkipsUploadAndCachesFailure()
    {
        var creationCount = 0;
        using var service = new RtlTexturePresentationService(
            new Config(),
            (_, _) =>
            {
                Interlocked.Increment(ref creationCount);
                return Task.FromResult<IDalamudTextureWrap>(
                    new FakeTextureWrap(width: 1, height: 1));
            },
            maxConcurrentTextureCreations: 1);
        var request = CreateRequest(
            2,
            "ar",
            string.Join("\n", Enumerable.Repeat("line", 200)));

        Assert.Null(service.TryRender(request));
        Assert.True(SpinWait.SpinUntil(
            () => service.GetDebugStats().PendingTextureCount == 0,
            TimeSpan.FromSeconds(5)));
        Assert.Null(service.TryRender(request));

        Assert.Equal(0, Volatile.Read(ref creationCount));
        Assert.Equal(1, service.GetDebugStats().RetryStateCount);
        Assert.Equal(0, service.GetDebugStats().Count);
    }

    /// <summary>
    /// Creates a texture layout request for one language and sample text.
    /// </summary>
    /// <param name="languageId">The target language identifier.</param>
    /// <param name="languageCode">The target language code.</param>
    /// <param name="text">The sample text.</param>
    /// <param name="fontScale">The request font scale.</param>
    /// <param name="textColor">The request text color.</param>
    /// <returns>The configured layout request.</returns>
    private static TextLayoutRequest CreateRequest(
        int languageId,
        string languageCode,
        string text,
        float fontScale = 1f,
        Vector4? textColor = null)
    {
        return new TextLayoutRequest(
            text,
            languageId,
            languageCode,
            480f,
            fontScale,
            ShouldUseGeneralFont: false,
            textColor ?? Vector4.One,
            Vector4.Zero,
            TranslationOverlaySurfaceId.ItemDetail,
            CenterAligned: false);
    }

    /// <summary>
    /// Pumps draw-thread publication until the controlled upload is available.
    /// </summary>
    /// <param name="service">The presentation service.</param>
    /// <param name="request">The request whose block is expected.</param>
    /// <returns>The completed render block.</returns>
    private static RenderedTextBlock WaitForRenderedBlock(
        RtlTexturePresentationService service,
        TextLayoutRequest request)
    {
        RenderedTextBlock? renderedBlock = null;
        Assert.True(SpinWait.SpinUntil(
            () => (renderedBlock = service.TryRender(request)) != null,
            TimeSpan.FromSeconds(5)));
        return renderedBlock!;
    }

    /// <summary>
    /// Creates a continuation-safe boolean signal.
    /// </summary>
    /// <returns>The new signal.</returns>
    private static TaskCompletionSource<bool> NewSignal()
    {
        return new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Creates a continuation-safe cancellation-token signal.
    /// </summary>
    /// <returns>The new signal.</returns>
    private static TaskCompletionSource<CancellationToken> NewCancellationSignal()
    {
        return new TaskCompletionSource<CancellationToken>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Creates a continuation-safe texture-request signal.
    /// </summary>
    /// <returns>The new signal.</returns>
    private static TaskCompletionSource<TextureCreationRequest>
        NewCreationRequestSignal()
    {
        return new TaskCompletionSource<TextureCreationRequest>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Creates a continuation-safe texture completion signal.
    /// </summary>
    /// <returns>The new signal.</returns>
    private static TaskCompletionSource<IDalamudTextureWrap> NewTextureSignal()
    {
        return new TaskCompletionSource<IDalamudTextureWrap>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Provides a minimal texture wrapper for presentation service tests.
    /// </summary>
    private sealed class FakeTextureWrap : IDalamudTextureWrap
    {
        private int disposeCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="FakeTextureWrap"/> class.
        /// </summary>
        /// <param name="width">The fake texture width.</param>
        /// <param name="height">The fake texture height.</param>
        public FakeTextureWrap(int width, int height)
        {
            this.Width = width;
            this.Height = height;
        }

        /// <summary>
        /// Gets the thread-safe disposal count.
        /// </summary>
        public int DisposeCount => Volatile.Read(ref this.disposeCount);

        public ImTextureID Handle => default;

        public int Width { get; }

        public int Height { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            Interlocked.Increment(ref this.disposeCount);
        }
    }
}
