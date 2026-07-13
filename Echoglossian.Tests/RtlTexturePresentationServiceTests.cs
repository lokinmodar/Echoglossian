// <copyright file="RtlTexturePresentationServiceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

using Echoglossian.UIOverlays.TextPresentation;
using Echoglossian.UIOverlays.TranslationOverlay;

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
            request =>
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
        Assert.True(
            SpinWait.SpinUntil(
                () => service.GetDebugStats().PendingTextureCount == 0,
                TimeSpan.FromSeconds(5)));

        var completedResult = service.TryRender(request);

        Assert.NotNull(completedResult);
        Assert.Same(texture, completedResult.Texture);
        Assert.False(completedResult.RightAligned);
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
            _ => Task.FromResult<IDalamudTextureWrap>(
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
    /// Creates a texture layout request for one language and sample text.
    /// </summary>
    /// <param name="languageId">The target language identifier.</param>
    /// <param name="languageCode">The target language code.</param>
    /// <param name="text">The sample text.</param>
    /// <returns>The configured layout request.</returns>
    private static TextLayoutRequest CreateRequest(
        int languageId,
        string languageCode,
        string text)
    {
        return new TextLayoutRequest(
            text,
            languageId,
            languageCode,
            480f,
            1f,
            ShouldUseGeneralFont: false,
            Vector4.One,
            Vector4.Zero,
            TranslationOverlaySurfaceId.ItemDetail,
            CenterAligned: false);
    }

    /// <summary>
    /// Provides a minimal texture wrapper for presentation service tests.
    /// </summary>
    private sealed class FakeTextureWrap : IDalamudTextureWrap
    {
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

        public ImTextureID Handle => default;

        public int Width { get; }

        public int Height { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
