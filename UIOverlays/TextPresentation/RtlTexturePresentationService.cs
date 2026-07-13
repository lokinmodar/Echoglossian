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
  private const long DefaultSoftByteBudget = 32L * 1024L * 1024L;
  private const long DefaultHardByteBudget = 64L * 1024L * 1024L;
  private static readonly TimeSpan FailureCooldown = TimeSpan.FromSeconds(10);
  private readonly Config configuration;
  private readonly TextTextureCache textureCache;
  private readonly Func<TextureCreationRequest, Task<IDalamudTextureWrap>>
      createTextureAsync;
  private readonly int adaptiveWidthCacheCapacity;
  private readonly Dictionary<string, float> adaptiveHoverTooltipWidthByKey =
      new(StringComparer.Ordinal);
  private readonly LinkedList<string> adaptiveHoverTooltipWidthAccessOrder =
      new();
  private readonly object adaptiveWidthCacheLock = new();
  private readonly object lifecycleLock = new();
  private readonly ConcurrentDictionary<string, Lazy<Task>>
      pendingTextureCreations = new(StringComparer.Ordinal);
  private readonly ConcurrentDictionary<string, DateTime> retryAfterByKey =
      new(StringComparer.Ordinal);
  private bool disposed;
  private int generationEpoch;

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
        request => CreateTextureAsync(textureProvider, request),
        DefaultAdaptiveWidthCacheCapacity)
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
  internal RtlTexturePresentationService(
      Config configuration,
      Func<TextureCreationRequest, Task<IDalamudTextureWrap>> createTextureAsync,
      int adaptiveWidthCacheCapacity = DefaultAdaptiveWidthCacheCapacity)
  {
    this.configuration = configuration;
    this.createTextureAsync = createTextureAsync;
    this.adaptiveWidthCacheCapacity = Math.Max(
        1,
        adaptiveWidthCacheCapacity);
    this.textureCache = new TextTextureCache(
        maxCapacity: 128,
        inactivityTimeoutSeconds: 60,
        softByteBudget: DefaultSoftByteBudget,
        hardByteBudget: DefaultHardByteBudget);
  }

  /// <summary>
  /// Tries to produce a measured render block for the provided texture-backed
  /// presentation request.
  /// </summary>
  /// <param name="request">The presentation request.</param>
  /// <returns>
  /// The rendered block when cached generation has completed; otherwise,
  /// <see langword="null"/> while creation is pending or cooling down.
  /// </returns>
  public RenderedTextBlock? TryRender(TextLayoutRequest request)
  {
    var cacheKey = this.BuildCacheKey(request);
    if (this.pendingTextureCreations.ContainsKey(cacheKey))
    {
      return null;
    }

    if (this.retryAfterByKey.TryGetValue(cacheKey, out var retryAfter))
    {
      if (retryAfter > DateTime.UtcNow)
      {
        return null;
      }

      this.retryAfterByKey.TryRemove(cacheKey, out _);
    }

    if (this.TryGetCachedTexture(cacheKey, out var cachedTexture))
    {
      return this.CreateRenderedBlock(request, cachedTexture);
    }

    var creationRequest = this.BuildTextureCreationRequest(request);
    int scheduledEpoch;
    lock (this.lifecycleLock)
    {
      if (this.disposed)
      {
        return null;
      }

      scheduledEpoch = this.generationEpoch;
    }

    var pendingCreation = this.pendingTextureCreations.GetOrAdd(
        cacheKey,
        _ => new Lazy<Task>(
            () => Task.Run(
                () => this.CreateAndCacheTextureAsync(
                    cacheKey,
                    request,
                    creationRequest,
                    scheduledEpoch)),
            LazyThreadSafetyMode.ExecutionAndPublication));
    _ = pendingCreation.Value;
    return null;
  }

  /// <inheritdoc/>
  public void Dispose()
  {
    lock (this.lifecycleLock)
    {
      if (this.disposed)
      {
        return;
      }

      this.disposed = true;
      this.generationEpoch++;
      this.textureCache.Dispose();
    }

    lock (this.adaptiveWidthCacheLock)
    {
      this.adaptiveHoverTooltipWidthByKey.Clear();
      this.adaptiveHoverTooltipWidthAccessOrder.Clear();
    }

    this.retryAfterByKey.Clear();
    GC.SuppressFinalize(this);
  }

  /// <summary>
  /// Clears generated texture state.
  /// </summary>
  public void Clear()
  {
    lock (this.lifecycleLock)
    {
      if (this.disposed)
      {
        return;
      }

      this.generationEpoch++;
      this.textureCache.Clear();
    }

    lock (this.adaptiveWidthCacheLock)
    {
      this.adaptiveHoverTooltipWidthByKey.Clear();
      this.adaptiveHoverTooltipWidthAccessOrder.Clear();
    }

    this.retryAfterByKey.Clear();
  }

  /// <summary>
  /// Gets debug information about texture, measurement, and pending state.
  /// </summary>
  /// <returns>Current cache counts and estimated texture memory usage.</returns>
  public (
      int Count,
      long EstimatedMemoryBytes,
      int AdaptiveWidthCount,
      int PendingTextureCount) GetDebugStats()
  {
    (int Count, long EstimatedMemoryBytes) textureStats;
    lock (this.lifecycleLock)
    {
      textureStats = this.textureCache.GetDebugStats();
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
        this.pendingTextureCreations.Count);
  }

  /// <summary>
  /// Resolves the adaptive wrap width used for texture-backed hover tooltips.
  /// Results are cached by text, font, and viewport characteristics so
  /// measurement is paid at most once per retained layout input.
  /// </summary>
  /// <param name="request">The hover tooltip layout request.</param>
  /// <param name="viewportWidth">The current main-viewport width.</param>
  /// <returns>The resolved hover-tooltip wrap width.</returns>
  public float ResolveAdaptiveHoverTooltipMaxWidth(
      TextLayoutRequest request,
      float viewportWidth)
  {
    var cacheKey = this.BuildHoverTooltipWidthKey(
        request,
        viewportWidth);
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
        request,
        viewportWidth);
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
  /// Creates one texture asynchronously and inserts it into the bounded cache.
  /// </summary>
  /// <param name="cacheKey">The complete layout cache key.</param>
  /// <param name="request">The source presentation request.</param>
  /// <param name="creationRequest">The resolved rasterization inputs.</param>
  /// <param name="scheduledEpoch">The clear/dispose generation epoch.</param>
  /// <returns>A task representing the creation operation.</returns>
  private async Task CreateAndCacheTextureAsync(
      string cacheKey,
      TextLayoutRequest request,
      TextureCreationRequest creationRequest,
      int scheduledEpoch)
  {
    IDalamudTextureWrap? generatedTexture = null;
    var textureHandedToCache = false;
    try
    {
      generatedTexture = await this.createTextureAsync(creationRequest)
          .ConfigureAwait(false);
      if (generatedTexture == null)
      {
        throw new InvalidOperationException(
            "Texture creation returned no texture.");
      }

      lock (this.lifecycleLock)
      {
        if (this.disposed || this.generationEpoch != scheduledEpoch)
        {
          generatedTexture.Dispose();
          generatedTexture = null;
          return;
        }

        textureHandedToCache = true;
        var cachedTexture = this.textureCache.GetOrCreate(
            cacheKey,
            () => generatedTexture);
        if (!ReferenceEquals(cachedTexture, generatedTexture))
        {
          generatedTexture.Dispose();
        }

        generatedTexture = null;
      }

      this.retryAfterByKey.TryRemove(cacheKey, out _);
    }
    catch (Exception ex)
    {
      if (!textureHandedToCache)
      {
        generatedTexture?.Dispose();
      }

      bool shouldReport;
      lock (this.lifecycleLock)
      {
        shouldReport =
            !this.disposed && this.generationEpoch == scheduledEpoch;
      }

      if (shouldReport)
      {
        this.retryAfterByKey[cacheKey] = DateTime.UtcNow + FailureCooldown;
        PluginRuntimeLog.Warning(
            $"Failed to generate text texture for surface '{request.SurfaceId}' and language '{request.LanguageCode}': {ex.Message}");
      }
    }
    finally
    {
      this.pendingTextureCreations.TryRemove(cacheKey, out _);
    }
  }

  /// <summary>
  /// Tries to read an existing texture without invoking synchronous creation.
  /// </summary>
  /// <param name="cacheKey">The complete layout cache key.</param>
  /// <param name="texture">The cached texture when found.</param>
  /// <returns><see langword="true"/> when the texture is cached.</returns>
  private bool TryGetCachedTexture(
      string cacheKey,
      out IDalamudTextureWrap texture)
  {
    lock (this.lifecycleLock)
    {
      if (this.disposed)
      {
        texture = null!;
        return false;
      }

      try
      {
        texture = this.textureCache.GetOrCreate(
            cacheKey,
            static () => throw new TextureCacheMissException());
        return true;
      }
      catch (TextureCacheMissException)
      {
        texture = null!;
        return false;
      }
    }
  }

  /// <summary>
  /// Creates the measured presentation block for a cached texture.
  /// </summary>
  /// <param name="request">The source presentation request.</param>
  /// <param name="texture">The cached texture.</param>
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
  /// Resolves the complete rasterization inputs for one request.
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

  private string BuildCacheKey(TextLayoutRequest request)
  {
    var fontPath = this.ResolveFontPath(request);
    var fontSize = this.ResolveFontSize(request).ToString(
        "0.###",
        CultureInfo.InvariantCulture);
    var maxWidth = this.ResolveMaxWidth(request)?.ToString(
        CultureInfo.InvariantCulture) ?? "none";
    var lineHeightScale = Math.Clamp(
        this.configuration.TexturePresentationLineHeightScale,
        0.8f,
        1.2f).ToString("0.###", CultureInfo.InvariantCulture);
    var textColor = this.SerializeColor(request.TextColor);
    var backgroundColor = this.SerializeColor(request.BackgroundColor);
    var direction = LanguagePresentationPolicy.ShouldRightAlign(request.LanguageId)
        ? "rtl"
        : "ltr";

    return string.Join(
        "|",
        request.LanguageId.ToString(CultureInfo.InvariantCulture),
        request.LanguageCode,
        request.SurfaceId.ToString(),
        fontPath,
        fontSize,
        maxWidth,
        lineHeightScale,
        direction,
        request.ShouldUseGeneralFont ? "general" : "language",
        request.CenterAligned ? "center" : "edge",
        textColor,
        backgroundColor,
        request.Text);
  }

  private string BuildHoverTooltipWidthKey(
      TextLayoutRequest request,
      float viewportWidth)
  {
    var fontPath = this.ResolveFontPath(request);
    var fontSize = this.ResolveFontSize(request).ToString(
        "0.###",
        CultureInfo.InvariantCulture);
    var lineHeightScale = Math.Clamp(
        this.configuration.TexturePresentationLineHeightScale,
        0.8f,
        1.2f).ToString("0.###", CultureInfo.InvariantCulture);
    var viewportWidthKey = Math.Round(
        viewportWidth,
        0,
        MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
    var configuredWidthKey = Math.Max(
        240f,
        this.configuration.HoverTooltipMaxWidth).ToString(
        "0.###",
        CultureInfo.InvariantCulture);

    return string.Join(
        "|",
        "hover-tooltip-width",
        request.LanguageId.ToString(CultureInfo.InvariantCulture),
        request.LanguageCode,
        fontPath,
        fontSize,
        lineHeightScale,
        configuredWidthKey,
        viewportWidthKey,
        request.ShouldUseGeneralFont ? "general" : "language",
        request.Text);
  }

  private float ResolveAdaptiveHoverTooltipMaxWidthCore(
      TextLayoutRequest request,
      float viewportWidth)
  {
    TextImageRenderer? renderer = null;
    try
    {
      return HoverTooltipLayoutPolicy.ResolveTextureMaxWidth(
          this.configuration,
          viewportWidth,
          request.Text,
          width =>
          {
            renderer ??= new TextImageRenderer(
                this.ResolveFontPath(request),
                this.ResolveFontSize(request),
                FontStyle.Regular,
                Math.Clamp(
                    this.configuration.TexturePresentationLineHeightScale,
                    0.8f,
                    1.2f),
                LanguagePresentationPolicy.ShouldRightAlign(
                    request.LanguageId));
            return this.MeasureTooltipHeight(
                renderer,
                request.Text,
                width);
          });
    }
    finally
    {
      renderer?.Dispose();
    }
  }

  private float MeasureTooltipHeight(
      TextImageRenderer renderer,
      string text,
      float width)
  {
    var measuredSize = renderer.MeasureShapedText(
        text,
        Math.Max(1, (int)Math.Ceiling(width)));
    return measuredSize.Height;
  }

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

    return DummyFontFilePath;
  }

  private float ResolveFontSize(TextLayoutRequest request)
  {
    return Math.Max(1.0f, this.configuration.FontSize * request.FontScale);
  }

  private int? ResolveMaxWidth(TextLayoutRequest request)
  {
    if (request.MaxWidth <= 0f)
    {
      return null;
    }

    return Math.Max(1, (int)Math.Ceiling(request.MaxWidth));
  }

  private string SerializeColor(Vector4 color)
  {
    return string.Create(
        CultureInfo.InvariantCulture,
        $"{color.X:0.###},{color.Y:0.###},{color.Z:0.###},{color.W:0.###}");
  }

  private Color ToColor(Vector4 color)
  {
    var alpha = this.ClampColorChannel(color.W);
    var red = this.ClampColorChannel(color.X);
    var green = this.ClampColorChannel(color.Y);
    var blue = this.ClampColorChannel(color.Z);
    return Color.FromArgb(alpha, red, green, blue);
  }

  private int ClampColorChannel(float value)
  {
    return (int)Math.Round(Math.Clamp(value, 0f, 1f) * 255f);
  }

  /// <summary>
  /// Rasterizes, encodes, and uploads one text texture.
  /// </summary>
  /// <param name="textureProvider">The Dalamud texture provider.</param>
  /// <param name="request">The resolved rasterization inputs.</param>
  /// <returns>The uploaded texture.</returns>
  private static async Task<IDalamudTextureWrap> CreateTextureAsync(
      ITextureProvider textureProvider,
      TextureCreationRequest request)
  {
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
    using MemoryStream stream = new();
    bitmap.Save(stream, ImageFormat.Png);
    stream.Position = 0;
    return await textureProvider.CreateFromImageAsync(stream)
        .ConfigureAwait(false);
  }

  /// <summary>
  /// Signals a non-generating cache probe miss.
  /// </summary>
  private sealed class TextureCacheMissException : Exception
  {
  }
}
