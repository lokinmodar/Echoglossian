// <copyright file="RtlTexturePresentationService.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TextPresentation;

/// <summary>
/// Captures all layout inputs required to create one text texture.
/// </summary>
/// <param name="Text">The logical text to rasterize.</param>
/// <param name="FontPath">The font file used for rasterization.</param>
/// <param name="FontSize">The rasterized font size.</param>
/// <param name="TextColor">The rasterized foreground color.</param>
/// <param name="BackgroundColor">The rasterized background color.</param>
/// <param name="MaxWidth">The optional wrapping width.</param>
/// <param name="LineHeightScale">The multiline line-height scale.</param>
/// <param name="RightToLeft">Whether the text uses RTL direction.</param>
internal sealed record TextureCreationRequest(
    string Text,
    string FontPath,
    float FontSize,
    Color TextColor,
    Color BackgroundColor,
    int? MaxWidth,
    float LineHeightScale,
    bool RightToLeft);

/// <summary>
/// Generates and caches texture-backed render blocks for complex-script text.
/// </summary>
internal sealed class RtlTexturePresentationService : IDisposable
{
  private const int DefaultAdaptiveWidthCacheCapacity = 128;
  private const int DefaultMaxConcurrentTextureCreations = 2;
  private const int DefaultPendingTextureCapacity = 16;
  private const int DefaultRetryStateCapacity = 128;
  private const int DefaultTextureCacheCapacity = 128;
  private const long DefaultSoftByteBudget = 32L * 1024L * 1024L;
  private static readonly TimeSpan FailureCooldown = TimeSpan.FromSeconds(10);
  private readonly Config configuration;
  private readonly TextTextureCache textureCache;
  private readonly Func<
      TextureCreationRequest,
      CancellationToken,
      Task<IDalamudTextureWrap>> createTextureAsync;
  private readonly int adaptiveWidthCacheCapacity;
  private readonly int maxConcurrentTextureCreations;
  private readonly int pendingTextureCapacity;
  private readonly int retryStateCapacity;
  private readonly Func<DateTime> getUtcNow;
  private readonly Dictionary<string, float> adaptiveHoverTooltipWidthByKey =
      new(StringComparer.Ordinal);
  private readonly LinkedList<string> adaptiveHoverTooltipWidthAccessOrder =
      new();
  private readonly object adaptiveWidthCacheLock = new();
  private readonly object lifecycleLock = new();
  private readonly Dictionary<string, RetryState> retryAfterByKey =
      new(StringComparer.Ordinal);
  private readonly LinkedList<string> retryAccessOrder = new();
  private readonly Queue<RetainedTexture> deferredTextureLeaseReleases = new();
  private GenerationState currentGeneration = new(0);
  private int activeTextureWorkerCount;
  private bool disposed;

  /// <summary>
  /// Initializes a new instance of the <see cref="RtlTexturePresentationService"/> class.
  /// </summary>
  /// <param name="configuration">The live plugin configuration.</param>
  /// <param name="textureProvider">The texture provider used to upload images.</param>
  public RtlTexturePresentationService(
      Config configuration,
      ITextureProvider textureProvider)
    : this(
        configuration,
        (request, cancellationToken) => CreateTextureAsync(
            textureProvider,
            request,
            cancellationToken))
  {
  }

  /// <summary>
  /// Initializes a testable instance with an injected texture creation seam.
  /// </summary>
  /// <param name="configuration">The live plugin configuration.</param>
  /// <param name="createTextureAsync">
  /// The asynchronous texture creation operation.
  /// </param>
  /// <param name="adaptiveWidthCacheCapacity">
  /// The maximum number of adaptive-width measurements to retain.
  /// </param>
  /// <param name="textureCacheCapacity">
  /// The maximum number of completed textures to retain.
  /// </param>
  /// <param name="pendingTextureCapacity">
  /// The maximum number of queued and running texture requests.
  /// </param>
  /// <param name="maxConcurrentTextureCreations">
  /// The maximum number of texture workers for one lifecycle generation.
  /// </param>
  /// <param name="retryStateCapacity">
  /// The maximum number of failed-key cooldown entries to retain.
  /// </param>
  /// <param name="getUtcNow">The clock used for retry cooldowns.</param>
  internal RtlTexturePresentationService(
      Config configuration,
      Func<
          TextureCreationRequest,
          CancellationToken,
          Task<IDalamudTextureWrap>> createTextureAsync,
      int adaptiveWidthCacheCapacity = DefaultAdaptiveWidthCacheCapacity,
      int textureCacheCapacity = DefaultTextureCacheCapacity,
      int pendingTextureCapacity = DefaultPendingTextureCapacity,
      int maxConcurrentTextureCreations =
          DefaultMaxConcurrentTextureCreations,
      int retryStateCapacity = DefaultRetryStateCapacity,
      Func<DateTime>? getUtcNow = null)
  {
    this.configuration = configuration;
    this.createTextureAsync = createTextureAsync;
    this.adaptiveWidthCacheCapacity = Math.Max(
        1,
        adaptiveWidthCacheCapacity);
    this.pendingTextureCapacity = Math.Max(1, pendingTextureCapacity);
    this.maxConcurrentTextureCreations = Math.Max(
        1,
        Math.Min(maxConcurrentTextureCreations, this.pendingTextureCapacity));
    this.retryStateCapacity = Math.Max(1, retryStateCapacity);
    this.getUtcNow = getUtcNow ?? (() => DateTime.UtcNow);
    this.textureCache = new TextTextureCache(
        maxCapacity: Math.Max(1, textureCacheCapacity),
        inactivityTimeoutSeconds: 60,
        softByteBudget: DefaultSoftByteBudget,
        hardByteBudget: TextRasterLimits.MaximumTextureBytes);
  }

  /// <summary>
  /// Tries to produce a measured render block for the provided texture-backed
  /// presentation request.
  /// </summary>
  /// <param name="request">The presentation request.</param>
  /// <returns>
  /// The rendered block when cached generation has completed; otherwise,
  /// <see langword="null"/> while creation is pending, cooling down, or the
  /// bounded work queue is full.
  /// </returns>
  public RenderedTextBlock? TryRender(TextLayoutRequest request)
  {
    var creationRequest = this.BuildTextureCreationRequest(request);
    var cacheKey = BuildCacheKey(creationRequest);

    lock (this.lifecycleLock)
    {
      if (this.disposed)
      {
        return null;
      }

      var generation = this.currentGeneration;
      this.PublishCompletedTextures(generation);

      if (this.TryGetCachedTextureLease(cacheKey, out var cachedTexture))
      {
        return this.CreateRenderedBlock(request, cachedTexture);
      }

      var now = this.getUtcNow();
      this.PruneExpiredRetryState(now);
      if (this.retryAfterByKey.TryGetValue(cacheKey, out var retryState) &&
          retryState.RetryAfterUtc > now)
      {
        this.TouchRetryState(retryState);
        return null;
      }

      if (generation.PendingByKey.ContainsKey(cacheKey) ||
          generation.PendingByKey.Count >= this.pendingTextureCapacity)
      {
        return null;
      }

      var work = new PendingTextureCreation(
          cacheKey,
          request.SurfaceId,
          request.LanguageCode,
          creationRequest);
      generation.PendingByKey.Add(cacheKey, work);
      generation.Queue.Enqueue(work);
      this.EnsureTextureWorkers(generation);
      return null;
    }
  }

  /// <summary>
  /// Begins one host draw frame and releases leases submitted during the prior
  /// frame, after the host has completed that frame's command processing.
  /// </summary>
  public void BeginDrawFrame()
  {
    List<RetainedTexture> releases;
    lock (this.lifecycleLock)
    {
      releases = this.DrainDeferredTextureLeaseReleases();
    }

    foreach (var retainedTexture in releases)
    {
      retainedTexture.Release();
    }
  }

  /// <inheritdoc/>
  public void Dispose()
  {
    GenerationState retiredGeneration;
    List<RetainedTexture> deferredReleases;
    lock (this.lifecycleLock)
    {
      if (this.disposed)
      {
        return;
      }

      this.disposed = true;
      retiredGeneration = this.currentGeneration;
      this.DiscardPendingTextureWork(retiredGeneration);
      this.DisposeCompletedTextures(retiredGeneration);
      this.textureCache.Dispose();
      this.ClearRetryState();
      deferredReleases = this.DrainDeferredTextureLeaseReleases();
    }

    retiredGeneration.Cancellation.Cancel();
    foreach (var retainedTexture in deferredReleases)
    {
      retainedTexture.Release();
    }

    this.ClearAdaptiveWidthState();
    GC.SuppressFinalize(this);
  }

  /// <summary>
  /// Clears generated texture state and atomically starts a new work generation.
  /// </summary>
  public void Clear()
  {
    GenerationState retiredGeneration;
    lock (this.lifecycleLock)
    {
      if (this.disposed)
      {
        return;
      }

      retiredGeneration = this.currentGeneration;
      this.currentGeneration = new GenerationState(
          retiredGeneration.Epoch + 1);
      this.DiscardPendingTextureWork(retiredGeneration);
      this.DisposeCompletedTextures(retiredGeneration);
      this.textureCache.Clear();
      this.ClearRetryState();
    }

    retiredGeneration.Cancellation.Cancel();
    this.ClearAdaptiveWidthState();
  }

  /// <summary>
  /// Gets debug information about texture, measurement, pending, and retry state.
  /// </summary>
  /// <returns>Current cache counts and estimated texture memory usage.</returns>
  public (
      int Count,
      long EstimatedMemoryBytes,
      int AdaptiveWidthCount,
      int PendingTextureCount,
      int RetryStateCount,
      int QueuedTextureCount,
      int ActiveTextureWorkerCount) GetDebugStats()
  {
    (int Count, long EstimatedMemoryBytes) textureStats;
    int pendingTextureCount;
    int retryStateCount;
    int queuedTextureCount;
    int activeWorkerCount;
    lock (this.lifecycleLock)
    {
      textureStats = this.textureCache.GetDebugStats();
      pendingTextureCount = this.currentGeneration.PendingByKey.Count;
      retryStateCount = this.retryAfterByKey.Count;
      queuedTextureCount = this.currentGeneration.Queue.Count;
      activeWorkerCount = this.activeTextureWorkerCount;
    }

    int adaptiveWidthCount;
    lock (this.adaptiveWidthCacheLock)
    {
      adaptiveWidthCount = this.adaptiveHoverTooltipWidthByKey.Count;
    }

    return (
        textureStats.Count,
        textureStats.EstimatedMemoryBytes,
        adaptiveWidthCount,
        pendingTextureCount,
        retryStateCount,
        queuedTextureCount,
        activeWorkerCount);
  }

  /// <summary>
  /// Resolves the adaptive wrap width used for texture-backed hover tooltips.
  /// </summary>
  /// <param name="request">The hover tooltip layout request.</param>
  /// <param name="viewportWidth">The current main-viewport width.</param>
  /// <returns>The resolved hover-tooltip wrap width.</returns>
  public float ResolveAdaptiveHoverTooltipMaxWidth(
      TextLayoutRequest request,
      float viewportWidth)
  {
    var resolvedInputs = this.BuildAdaptiveWidthInputs(
        request,
        viewportWidth);
    var cacheKey = BuildHoverTooltipWidthKey(resolvedInputs);
    lock (this.adaptiveWidthCacheLock)
    {
      if (this.adaptiveHoverTooltipWidthByKey.TryGetValue(
              cacheKey,
              out var cachedWidth))
      {
        this.adaptiveHoverTooltipWidthAccessOrder.Remove(cacheKey);
        this.adaptiveHoverTooltipWidthAccessOrder.AddLast(cacheKey);
        return cachedWidth;
      }
    }

    var resolvedWidth = this.ResolveAdaptiveHoverTooltipMaxWidthCore(
        resolvedInputs);
    lock (this.adaptiveWidthCacheLock)
    {
      if (this.adaptiveHoverTooltipWidthByKey.TryGetValue(
              cacheKey,
              out var concurrentlyCachedWidth))
      {
        this.adaptiveHoverTooltipWidthAccessOrder.Remove(cacheKey);
        this.adaptiveHoverTooltipWidthAccessOrder.AddLast(cacheKey);
        return concurrentlyCachedWidth;
      }

      while (this.adaptiveHoverTooltipWidthByKey.Count >=
             this.adaptiveWidthCacheCapacity &&
             this.adaptiveHoverTooltipWidthAccessOrder.First is { } oldestKey)
      {
        this.adaptiveHoverTooltipWidthByKey.Remove(oldestKey.Value);
        this.adaptiveHoverTooltipWidthAccessOrder.RemoveFirst();
      }

      this.adaptiveHoverTooltipWidthByKey.Add(cacheKey, resolvedWidth);
      this.adaptiveHoverTooltipWidthAccessOrder.AddLast(cacheKey);
    }

    return resolvedWidth;
  }

  /// <summary>
  /// Starts only enough workers to service queued work within the concurrency cap.
  /// </summary>
  /// <param name="generation">The active lifecycle generation.</param>
  private void EnsureTextureWorkers(GenerationState generation)
  {
    var workersToStart = Math.Min(
        this.maxConcurrentTextureCreations - this.activeTextureWorkerCount,
        generation.Queue.Count);
    this.activeTextureWorkerCount += workersToStart;
    generation.ActiveWorkerCount += workersToStart;
    for (var workerIndex = 0; workerIndex < workersToStart; workerIndex++)
    {
      _ = Task.Run(() => this.ProcessTextureQueueAsync(generation));
    }
  }

  /// <summary>
  /// Processes queued texture work for one lifecycle generation.
  /// </summary>
  /// <param name="generation">The generation owned by this worker.</param>
  /// <returns>A task representing worker completion.</returns>
  private async Task ProcessTextureQueueAsync(GenerationState generation)
  {
    while (true)
    {
      PendingTextureCreation work;
      Task<IDalamudTextureWrap> creationTask;
      CancellationToken cancellationToken;
      lock (this.lifecycleLock)
      {
        if (this.disposed ||
            !ReferenceEquals(this.currentGeneration, generation) ||
            generation.Cancellation.IsCancellationRequested ||
            !generation.Queue.TryDequeue(out work!))
        {
          this.CompleteTextureWorker(generation);
          return;
        }

        cancellationToken = generation.Cancellation.Token;
      }

      try
      {
        creationTask = this.CreateTextureWithinRasterLimitsAsync(
            work.CreationRequest,
            cancellationToken);
      }
      catch (Exception ex)
      {
        creationTask = Task.FromException<IDalamudTextureWrap>(ex);
      }

      IDalamudTextureWrap? generatedTexture = null;
      Exception? creationException = null;
      try
      {
        generatedTexture = await creationTask.ConfigureAwait(false);
        if (generatedTexture == null)
        {
          throw new InvalidOperationException(
              "Texture creation returned no texture.");
        }
      }
      catch (Exception ex)
      {
        creationException = ex;
      }

      var shouldReportFailure = false;
      lock (this.lifecycleLock)
      {
        var isCurrentGeneration =
            !this.disposed &&
            ReferenceEquals(this.currentGeneration, generation) &&
            !generation.Cancellation.IsCancellationRequested;
        if (isCurrentGeneration &&
            generation.PendingByKey.TryGetValue(work.CacheKey, out var pending) &&
            ReferenceEquals(pending, work))
        {
          generation.PendingByKey.Remove(work.CacheKey);
          if (creationException == null)
          {
            generation.Completed.Enqueue(
                new CompletedTextureCreation(work, generatedTexture!));
            generatedTexture = null;
          }
          else if (creationException is not OperationCanceledException)
          {
            this.AddRetryState(
                work.CacheKey,
                this.getUtcNow() + FailureCooldown);
            shouldReportFailure = true;
          }
        }
      }

      generatedTexture?.Dispose();
      if (shouldReportFailure)
      {
        LogCreationFailure(work, creationException!);
      }
    }
  }

  /// <summary>
  /// Retires one globally counted worker and starts eligible current work when
  /// capacity becomes available.
  /// </summary>
  /// <param name="generation">The generation whose worker is ending.</param>
  private void CompleteTextureWorker(GenerationState generation)
  {
    generation.ActiveWorkerCount--;
    this.activeTextureWorkerCount--;
    if (!this.disposed)
    {
      this.EnsureTextureWorkers(this.currentGeneration);
    }
  }

  /// <summary>
  /// Removes all queued and keyed work owned by a retired generation.
  /// Running provider calls remain cancellation-bound and globally capped.
  /// </summary>
  /// <param name="generation">The generation being retired.</param>
  private void DiscardPendingTextureWork(GenerationState generation)
  {
    generation.Queue.Clear();
    generation.PendingByKey.Clear();
  }

  /// <summary>
  /// Publishes completed uploads into the LRU on the requesting draw thread.
  /// </summary>
  /// <param name="generation">The active lifecycle generation.</param>
  private void PublishCompletedTextures(GenerationState generation)
  {
    while (generation.Completed.TryDequeue(out var completion))
    {
      var owner = new RetainedTexture(completion.Texture);
      try
      {
        var cachedTexture = this.textureCache.GetOrCreate(
            completion.Work.CacheKey,
            () => owner);
        if (!ReferenceEquals(cachedTexture, owner))
        {
          owner.Dispose();
        }

        this.RemoveRetryState(completion.Work.CacheKey);
      }
      catch (Exception ex)
      {
        owner.Dispose();
        this.AddRetryState(
            completion.Work.CacheKey,
            this.getUtcNow() + FailureCooldown);
        LogCreationFailure(completion.Work, ex);
      }
    }
  }

  /// <summary>
  /// Disposes uploads that completed before a lifecycle transition but were not published.
  /// </summary>
  /// <param name="generation">The retired generation.</param>
  private void DisposeCompletedTextures(GenerationState generation)
  {
    while (generation.Completed.TryDequeue(out var completion))
    {
      completion.Texture.Dispose();
    }
  }

  /// <summary>
  /// Tries to acquire a draw lease for an existing cached texture.
  /// </summary>
  /// <param name="cacheKey">The exact raster input key.</param>
  /// <param name="texture">The leased texture when found.</param>
  /// <returns><see langword="true"/> when the texture is cached.</returns>
  private bool TryGetCachedTextureLease(
      string cacheKey,
      out IDalamudTextureWrap texture)
  {
    try
    {
      var cachedTexture = this.textureCache.GetOrCreate(
          cacheKey,
          static () => throw new TextureCacheMissException());
      if (cachedTexture is not RetainedTexture retainedTexture)
      {
        throw new InvalidOperationException(
            "Texture cache entry does not support draw leasing.");
      }

      texture = retainedTexture.Acquire(this.DeferTextureLeaseRelease);
      return true;
    }
    catch (TextureCacheMissException)
    {
      texture = null!;
      return false;
    }
  }

  /// <summary>
  /// Defers one submitted draw lease until a later host draw frame begins.
  /// </summary>
  /// <param name="retainedTexture">The retained cache owner.</param>
  private void DeferTextureLeaseRelease(RetainedTexture retainedTexture)
  {
    var releaseImmediately = false;
    lock (this.lifecycleLock)
    {
      if (this.disposed)
      {
        releaseImmediately = true;
      }
      else
      {
        this.deferredTextureLeaseReleases.Enqueue(retainedTexture);
      }
    }

    if (releaseImmediately)
    {
      retainedTexture.Release();
    }
  }

  /// <summary>
  /// Drains texture leases whose submitting host frame has completed.
  /// </summary>
  /// <returns>The retained owners ready for release.</returns>
  private List<RetainedTexture> DrainDeferredTextureLeaseReleases()
  {
    var releases = new List<RetainedTexture>(
        this.deferredTextureLeaseReleases.Count);
    while (this.deferredTextureLeaseReleases.TryDequeue(
               out var retainedTexture))
    {
      releases.Add(retainedTexture);
    }

    return releases;
  }

  /// <summary>
  /// Creates the measured presentation block for a leased texture.
  /// </summary>
  /// <param name="request">The source presentation request.</param>
  /// <param name="texture">The leased cached texture.</param>
  /// <returns>The completed rendered block.</returns>
  private RenderedTextBlock CreateRenderedBlock(
      TextLayoutRequest request,
      IDalamudTextureWrap texture)
  {
    return new RenderedTextBlock(
        TextPresentationBackendKind.RtlTexture,
        new Vector2(texture.Width, texture.Height),
        texture,
        rightAligned:
            LanguagePresentationPolicy.ShouldRightAlign(request.LanguageId));
  }

  /// <summary>
  /// Resolves the complete rasterization inputs exactly once for one request.
  /// </summary>
  /// <param name="request">The source presentation request.</param>
  /// <returns>The direction-aware texture creation request.</returns>
  private TextureCreationRequest BuildTextureCreationRequest(
      TextLayoutRequest request)
  {
    return new TextureCreationRequest(
        request.Text,
        this.ResolveFontPath(request),
        this.ResolveFontSize(request),
        this.ToColor(request.TextColor),
        this.ToColor(request.BackgroundColor),
        this.ResolveMaxWidth(request),
        Math.Clamp(
            this.configuration.TexturePresentationLineHeightScale,
            0.8f,
            1.2f),
        LanguagePresentationPolicy.ShouldRightAlign(request.LanguageId));
  }

  /// <summary>
  /// Builds a key from the exact resolved values supplied to rasterization.
  /// </summary>
  /// <param name="request">The resolved raster request.</param>
  /// <returns>The complete raster cache key.</returns>
  private static string BuildCacheKey(TextureCreationRequest request)
  {
    return string.Join(
        "|",
        SerializeKeyString(request.FontPath),
        SerializeFloatBits(request.FontSize),
        request.TextColor.ToArgb().ToString("X8", CultureInfo.InvariantCulture),
        request.BackgroundColor.ToArgb().ToString(
            "X8",
            CultureInfo.InvariantCulture),
        request.MaxWidth?.ToString(CultureInfo.InvariantCulture) ?? "none",
        SerializeFloatBits(request.LineHeightScale),
        request.RightToLeft ? "rtl" : "ltr",
        SerializeKeyString(request.Text));
  }

  /// <summary>
  /// Captures every input used by adaptive tooltip width resolution.
  /// </summary>
  /// <param name="request">The source layout request.</param>
  /// <param name="viewportWidth">The exact viewport width.</param>
  /// <returns>The resolved measurement inputs.</returns>
  private AdaptiveWidthInputs BuildAdaptiveWidthInputs(
      TextLayoutRequest request,
      float viewportWidth)
  {
    return new AdaptiveWidthInputs(
        request.Text,
        this.ResolveFontPath(request),
        this.ResolveFontSize(request),
        Math.Clamp(
            this.configuration.TexturePresentationLineHeightScale,
            0.8f,
            1.2f),
        LanguagePresentationPolicy.ShouldRightAlign(request.LanguageId),
        Math.Clamp(
            viewportWidth,
            1f,
            TextRasterLimits.MaximumDimension),
        Math.Min(
            this.configuration.HoverTooltipMaxWidth,
            TextRasterLimits.MaximumDimension));
  }

  /// <summary>
  /// Builds an adaptive-width key from the exact values used by measurement.
  /// </summary>
  /// <param name="inputs">The captured measurement inputs.</param>
  /// <returns>The complete adaptive-width cache key.</returns>
  private static string BuildHoverTooltipWidthKey(AdaptiveWidthInputs inputs)
  {
    return string.Join(
        "|",
        "hover-tooltip-width",
        SerializeKeyString(inputs.FontPath),
        SerializeFloatBits(inputs.FontSize),
        SerializeFloatBits(inputs.LineHeightScale),
        inputs.RightToLeft ? "rtl" : "ltr",
        SerializeFloatBits(inputs.ViewportWidth),
        SerializeFloatBits(inputs.ConfiguredMaxWidth),
        SerializeKeyString(inputs.Text));
  }

  /// <summary>
  /// Resolves adaptive width using the captured measurement inputs.
  /// </summary>
  /// <param name="inputs">The exact measurement inputs.</param>
  /// <returns>The resolved tooltip width.</returns>
  private float ResolveAdaptiveHoverTooltipMaxWidthCore(
      AdaptiveWidthInputs inputs)
  {
    var resolvedConfiguration = new Config
    {
      HoverTooltipMaxWidth = inputs.ConfiguredMaxWidth,
    };
    TextImageRenderer? renderer = null;
    try
    {
      return Math.Min(
          TextRasterLimits.MaximumDimension,
          HoverTooltipLayoutPolicy.ResolveTextureMaxWidth(
          resolvedConfiguration,
          inputs.ViewportWidth,
          inputs.Text,
          width =>
          {
            renderer ??= new TextImageRenderer(
                inputs.FontPath,
                inputs.FontSize,
                FontStyle.Regular,
                inputs.LineHeightScale,
                inputs.RightToLeft);
            return this.MeasureTooltipHeight(
                renderer,
                inputs.Text,
                width);
          }));
    }
    finally
    {
      renderer?.Dispose();
    }
  }

  /// <summary>
  /// Measures tooltip height at one candidate width.
  /// </summary>
  /// <param name="renderer">The configured text renderer.</param>
  /// <param name="text">The text to measure.</param>
  /// <param name="width">The candidate width.</param>
  /// <returns>The measured height.</returns>
  private float MeasureTooltipHeight(
      TextImageRenderer renderer,
      string text,
      float width)
  {
    var measuredSize = renderer.MeasureShapedText(
        text,
        TextRasterLimits.ClampWrapWidth(
            Math.Max(1, (int)Math.Ceiling(width))));
    return measuredSize.Height;
  }

  /// <summary>
  /// Resolves the font file used for one request.
  /// </summary>
  /// <param name="request">The source layout request.</param>
  /// <returns>The resolved font path.</returns>
  private string ResolveFontPath(TextLayoutRequest request)
  {
    if (!request.ShouldUseGeneralFont &&
        !string.IsNullOrWhiteSpace(SpecialFontFilePath))
    {
      return SpecialFontFilePath;
    }

    if (!string.IsNullOrWhiteSpace(FontFilePath))
    {
      return FontFilePath;
    }

    return DummyFontFilePath ?? string.Empty;
  }

  /// <summary>
  /// Resolves the exact raster font size.
  /// </summary>
  /// <param name="request">The source layout request.</param>
  /// <returns>The resolved font size.</returns>
  private float ResolveFontSize(TextLayoutRequest request)
  {
    return Math.Max(1.0f, this.configuration.FontSize * request.FontScale);
  }

  /// <summary>
  /// Resolves the integer raster wrapping width.
  /// </summary>
  /// <param name="request">The source layout request.</param>
  /// <returns>The resolved width or <see langword="null"/>.</returns>
  private int? ResolveMaxWidth(TextLayoutRequest request)
  {
    if (request.MaxWidth <= 0f)
    {
      return null;
    }

    return TextRasterLimits.ClampWrapWidth(
        Math.Max(1, (int)Math.Ceiling(request.MaxWidth)));
  }

  /// <summary>
  /// Converts an ImGui color into the final GDI color supplied to rendering.
  /// </summary>
  /// <param name="color">The source color.</param>
  /// <returns>The resolved GDI color.</returns>
  private Color ToColor(Vector4 color)
  {
    var alpha = this.ClampColorChannel(color.W);
    var red = this.ClampColorChannel(color.X);
    var green = this.ClampColorChannel(color.Y);
    var blue = this.ClampColorChannel(color.Z);
    return Color.FromArgb(alpha, red, green, blue);
  }

  /// <summary>
  /// Converts one normalized color channel to its final byte value.
  /// </summary>
  /// <param name="value">The normalized channel.</param>
  /// <returns>The byte-range integer channel.</returns>
  private int ClampColorChannel(float value)
  {
    return (int)Math.Round(Math.Clamp(value, 0f, 1f) * 255f);
  }

  /// <summary>
  /// Adds or refreshes one bounded retry cooldown entry.
  /// </summary>
  /// <param name="cacheKey">The failed raster key.</param>
  /// <param name="retryAfterUtc">The next eligible creation time.</param>
  private void AddRetryState(string cacheKey, DateTime retryAfterUtc)
  {
    if (this.retryAfterByKey.TryGetValue(cacheKey, out var existingState))
    {
      this.retryAccessOrder.Remove(existingState.AccessNode);
      this.retryAfterByKey.Remove(cacheKey);
    }

    while (this.retryAfterByKey.Count >= this.retryStateCapacity &&
           this.retryAccessOrder.First is { } oldestNode)
    {
      this.retryAfterByKey.Remove(oldestNode.Value);
      this.retryAccessOrder.RemoveFirst();
    }

    var accessNode = this.retryAccessOrder.AddLast(cacheKey);
    this.retryAfterByKey.Add(
        cacheKey,
        new RetryState(retryAfterUtc, accessNode));
  }

  /// <summary>
  /// Moves one retry entry to the most-recently-used position.
  /// </summary>
  /// <param name="retryState">The retry state to touch.</param>
  private void TouchRetryState(RetryState retryState)
  {
    this.retryAccessOrder.Remove(retryState.AccessNode);
    this.retryAccessOrder.AddLast(retryState.AccessNode);
  }

  /// <summary>
  /// Removes expired retry entries without waiting for the same text to return.
  /// </summary>
  /// <param name="now">The current UTC time.</param>
  private void PruneExpiredRetryState(DateTime now)
  {
    var node = this.retryAccessOrder.First;
    while (node != null)
    {
      var next = node.Next;
      if (this.retryAfterByKey.TryGetValue(node.Value, out var state) &&
          state.RetryAfterUtc <= now)
      {
        this.retryAfterByKey.Remove(node.Value);
        this.retryAccessOrder.Remove(node);
      }

      node = next;
    }
  }

  /// <summary>
  /// Removes one retry entry after successful publication.
  /// </summary>
  /// <param name="cacheKey">The successful raster key.</param>
  private void RemoveRetryState(string cacheKey)
  {
    if (!this.retryAfterByKey.Remove(cacheKey, out var retryState))
    {
      return;
    }

    this.retryAccessOrder.Remove(retryState.AccessNode);
  }

  /// <summary>
  /// Clears all retry cooldown state.
  /// </summary>
  private void ClearRetryState()
  {
    this.retryAfterByKey.Clear();
    this.retryAccessOrder.Clear();
  }

  /// <summary>
  /// Clears adaptive-width measurement state.
  /// </summary>
  private void ClearAdaptiveWidthState()
  {
    lock (this.adaptiveWidthCacheLock)
    {
      this.adaptiveHoverTooltipWidthByKey.Clear();
      this.adaptiveHoverTooltipWidthAccessOrder.Clear();
    }
  }

  /// <summary>
  /// Serializes a float without losing any input bits.
  /// </summary>
  /// <param name="value">The float value.</param>
  /// <returns>The exact bit-pattern key segment.</returns>
  private static string SerializeFloatBits(float value)
  {
    return BitConverter.SingleToInt32Bits(value).ToString(
        "X8",
        CultureInfo.InvariantCulture);
  }

  /// <summary>
  /// Serializes a string with its length so delimiters cannot alias key fields.
  /// </summary>
  /// <param name="value">The exact string value.</param>
  /// <returns>The length-prefixed key segment.</returns>
  private static string SerializeKeyString(string value)
  {
    return string.Create(
        CultureInfo.InvariantCulture,
        $"{value.Length}:{value}");
  }

  /// <summary>
  /// Logs one current-generation creation or publication failure.
  /// </summary>
  /// <param name="work">The failed work item.</param>
  /// <param name="exception">The failure.</param>
  private static void LogCreationFailure(
      PendingTextureCreation work,
      Exception exception)
  {
    PluginRuntimeLog.Warning(
        $"Failed to generate text texture for surface '{work.SurfaceId}' and language '{work.LanguageCode}': {exception.Message}");
  }

  /// <summary>
  /// Rasterizes, encodes, and uploads one text texture.
  /// </summary>
  /// <param name="textureProvider">The Dalamud texture provider.</param>
  /// <param name="request">The resolved rasterization inputs.</param>
  /// <param name="cancellationToken">The lifecycle cancellation token.</param>
  /// <returns>The uploaded texture.</returns>
  private static async Task<IDalamudTextureWrap> CreateTextureAsync(
      ITextureProvider textureProvider,
      TextureCreationRequest request,
      CancellationToken cancellationToken)
  {
    await Task.Yield();
    cancellationToken.ThrowIfCancellationRequested();
    using TextImageRenderer renderer = new(
        request.FontPath,
        request.FontSize,
        FontStyle.Regular,
        request.LineHeightScale,
        request.RightToLeft);
    using Bitmap bitmap = renderer.RenderShapedText(
        request.Text,
        request.TextColor,
        request.BackgroundColor,
        request.MaxWidth);
    cancellationToken.ThrowIfCancellationRequested();
    using MemoryStream stream = new();
    bitmap.Save(stream, ImageFormat.Png);
    cancellationToken.ThrowIfCancellationRequested();
    stream.Position = 0;
    return await textureProvider.CreateFromImageAsync(stream)
        .ConfigureAwait(false);
  }

  /// <summary>
  /// Measures a request before invoking the configured upload operation so an
  /// over-limit layout cannot allocate, encode, or upload a texture.
  /// </summary>
  /// <param name="request">The resolved rasterization inputs.</param>
  /// <param name="cancellationToken">The lifecycle cancellation token.</param>
  /// <returns>The uploaded texture.</returns>
  private async Task<IDalamudTextureWrap> CreateTextureWithinRasterLimitsAsync(
      TextureCreationRequest request,
      CancellationToken cancellationToken)
  {
    await Task.Yield();
    cancellationToken.ThrowIfCancellationRequested();
    using TextImageRenderer renderer = new(
        request.FontPath,
        request.FontSize,
        FontStyle.Regular,
        request.LineHeightScale,
        request.RightToLeft);
    var measuredSize = renderer.MeasureShapedText(
        request.Text,
        request.MaxWidth);
    if (!TextRasterLimits.IsWithinLimits(measuredSize))
    {
      throw new InvalidOperationException(
          "Text layout exceeds the bounded raster allocation limits.");
    }

    cancellationToken.ThrowIfCancellationRequested();
    return await this.createTextureAsync(request, cancellationToken)
        .ConfigureAwait(false);
  }

  /// <summary>
  /// Captures one lifecycle generation's queue and publication state.
  /// </summary>
  private sealed class GenerationState
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="GenerationState"/> class.
    /// </summary>
    /// <param name="epoch">The lifecycle generation number.</param>
    public GenerationState(int epoch)
    {
      this.Epoch = epoch;
    }

    public int Epoch { get; }

    public CancellationTokenSource Cancellation { get; } = new();

    public Queue<PendingTextureCreation> Queue { get; } = new();

    public Dictionary<string, PendingTextureCreation> PendingByKey { get; } =
        new(StringComparer.Ordinal);

    public Queue<CompletedTextureCreation> Completed { get; } = new();

    public int ActiveWorkerCount { get; set; }
  }

  /// <summary>
  /// Represents one admitted texture creation request.
  /// </summary>
  /// <param name="CacheKey">The exact raster cache key.</param>
  /// <param name="SurfaceId">The source surface for diagnostics.</param>
  /// <param name="LanguageCode">The source language for diagnostics.</param>
  /// <param name="CreationRequest">The exact raster inputs.</param>
  private sealed record PendingTextureCreation(
      string CacheKey,
      TranslationOverlaySurfaceId SurfaceId,
      string LanguageCode,
      TextureCreationRequest CreationRequest);

  /// <summary>
  /// Represents one uploaded texture awaiting draw-thread publication.
  /// </summary>
  /// <param name="Work">The originating work item.</param>
  /// <param name="Texture">The uploaded texture.</param>
  private sealed record CompletedTextureCreation(
      PendingTextureCreation Work,
      IDalamudTextureWrap Texture);

  /// <summary>
  /// Captures exact adaptive-width policy and measurement inputs.
  /// </summary>
  /// <param name="Text">The logical tooltip text.</param>
  /// <param name="FontPath">The resolved font path.</param>
  /// <param name="FontSize">The resolved font size.</param>
  /// <param name="LineHeightScale">The resolved line-height scale.</param>
  /// <param name="RightToLeft">Whether measurement uses RTL format.</param>
  /// <param name="ViewportWidth">The exact viewport width.</param>
  /// <param name="ConfiguredMaxWidth">The exact configured width cap.</param>
  private sealed record AdaptiveWidthInputs(
      string Text,
      string FontPath,
      float FontSize,
      float LineHeightScale,
      bool RightToLeft,
      float ViewportWidth,
      float ConfiguredMaxWidth);

  /// <summary>
  /// Stores one bounded retry cooldown entry.
  /// </summary>
  /// <param name="RetryAfterUtc">The next eligible creation time.</param>
  /// <param name="AccessNode">The retry LRU node.</param>
  private sealed record RetryState(
      DateTime RetryAfterUtc,
      LinkedListNode<string> AccessNode);

  /// <summary>
  /// Owns a cached texture and delays final disposal while draw leases exist.
  /// </summary>
  private sealed class RetainedTexture : IDalamudTextureWrap
  {
    private readonly object textureLock = new();
    private readonly int width;
    private readonly int height;
    private IDalamudTextureWrap? texture;
    private int leaseCount;
    private bool retired;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetainedTexture"/> class.
    /// </summary>
    /// <param name="texture">The owned uploaded texture.</param>
    public RetainedTexture(IDalamudTextureWrap texture)
    {
      this.texture = texture;
      this.width = texture.Width;
      this.height = texture.Height;
    }

    public ImTextureID Handle => this.GetHandle();

    public int Width => this.width;

    public int Height => this.height;

    /// <summary>
    /// Acquires one explicit draw lease.
    /// </summary>
    /// <returns>A texture wrapper that releases the lease on disposal.</returns>
    public IDalamudTextureWrap Acquire(Action<RetainedTexture> deferRelease)
    {
      lock (this.textureLock)
      {
        if (this.retired || this.texture == null)
        {
          throw new ObjectDisposedException(nameof(RetainedTexture));
        }

        this.leaseCount++;
        return new TextureLease(
            this,
            deferRelease,
            this.width,
            this.height);
      }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      IDalamudTextureWrap? textureToDispose = null;
      lock (this.textureLock)
      {
        if (this.retired)
        {
          return;
        }

        this.retired = true;
        if (this.leaseCount == 0)
        {
          textureToDispose = this.texture;
          this.texture = null;
        }
      }

      textureToDispose?.Dispose();
    }

    /// <summary>
    /// Gets the underlying texture handle while ownership is retained.
    /// </summary>
    /// <returns>The texture handle.</returns>
    public ImTextureID GetHandle()
    {
      lock (this.textureLock)
      {
        return (this.texture ??
            throw new ObjectDisposedException(nameof(RetainedTexture))).Handle;
      }
    }

    /// <summary>
    /// Releases one draw lease and performs deferred disposal when retired.
    /// </summary>
    public void Release()
    {
      IDalamudTextureWrap? textureToDispose = null;
      lock (this.textureLock)
      {
        if (this.leaseCount <= 0)
        {
          return;
        }

        this.leaseCount--;
        if (this.retired && this.leaseCount == 0)
        {
          textureToDispose = this.texture;
          this.texture = null;
        }
      }

      textureToDispose?.Dispose();
    }
  }

  /// <summary>
  /// Represents one caller-owned texture draw lease.
  /// </summary>
  private sealed class TextureLease : IDalamudTextureWrap
  {
    private readonly int width;
    private readonly int height;
    private readonly Action<RetainedTexture> deferRelease;
    private RetainedTexture? owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextureLease"/> class.
    /// </summary>
    /// <param name="owner">The retained cache owner.</param>
    /// <param name="deferRelease">The frame-aware release callback.</param>
    /// <param name="width">The texture width.</param>
    /// <param name="height">The texture height.</param>
    public TextureLease(
        RetainedTexture owner,
        Action<RetainedTexture> deferRelease,
        int width,
        int height)
    {
      this.owner = owner;
      this.deferRelease = deferRelease;
      this.width = width;
      this.height = height;
    }

    public ImTextureID Handle => (this.owner ??
        throw new ObjectDisposedException(nameof(TextureLease))).GetHandle();

    public int Width => this.width;

    public int Height => this.height;

    /// <inheritdoc/>
    public void Dispose()
    {
      var retainedTexture = Interlocked.Exchange(ref this.owner, null);
      if (retainedTexture != null)
      {
        this.deferRelease(retainedTexture);
      }
    }
  }

  /// <summary>
  /// Signals a non-generating cache probe miss.
  /// </summary>
  private sealed class TextureCacheMissException : Exception
  {
  }
}
