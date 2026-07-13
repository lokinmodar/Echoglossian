// <copyright file="RtlTexturePresentationService.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TextPresentation;

/// <summary>
/// Generates and caches texture-backed render blocks for complex-script text.
/// </summary>
internal sealed class RtlTexturePresentationService : IDisposable
{
  private const long DefaultSoftByteBudget = 32L * 1024L * 1024L;
  private const long DefaultHardByteBudget = 64L * 1024L * 1024L;
  private static readonly TimeSpan FailureCooldown = TimeSpan.FromSeconds(10);
  private readonly Config configuration;
  private readonly TextTextureCache textureCache;
  private readonly TextTextureGenerator textureGenerator;
  private readonly ConcurrentDictionary<string, float> adaptiveHoverTooltipWidthByKey =
      new(StringComparer.Ordinal);
  private readonly ConcurrentDictionary<string, DateTime> retryAfterByKey =
      new(StringComparer.Ordinal);

  /// <summary>
  /// Initializes a new instance of the <see cref="RtlTexturePresentationService"/> class.
  /// </summary>
  /// <param name="configuration">The live plugin configuration.</param>
  /// <param name="textureProvider">The texture provider used to upload images.</param>
  public RtlTexturePresentationService(
      Config configuration,
      ITextureProvider textureProvider)
  {
    this.configuration = configuration;
    this.textureGenerator = new TextTextureGenerator(textureProvider);
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
  /// The rendered block when generation succeeded; otherwise,
  /// <see langword="null"/>.
  /// </returns>
  public RenderedTextBlock? TryRender(TextLayoutRequest request)
  {
    var cacheKey = this.BuildCacheKey(request);
    if (this.retryAfterByKey.TryGetValue(cacheKey, out var retryAfter) &&
        retryAfter > DateTime.UtcNow)
    {
      return null;
    }

    try
    {
      var texture = this.textureCache.GetOrCreate(
          cacheKey,
          () => this.textureGenerator.CreateTextTextureAsync(
                  request.Text,
                  this.ResolveFontPath(request),
                  this.ResolveFontSize(request),
                  this.ToColor(request.TextColor),
                  this.ToColor(request.BackgroundColor),
                  FontStyle.Regular,
                  this.ResolveMaxWidth(request),
                  Math.Clamp(
                      this.configuration.TexturePresentationLineHeightScale,
                      0.8f,
                      1.2f))
              .GetAwaiter()
              .GetResult());

      this.retryAfterByKey.TryRemove(cacheKey, out _);
      return new RenderedTextBlock(
          TextPresentationBackendKind.RtlTexture,
          new Vector2(texture.Width, texture.Height),
          texture,
          rightAligned:
              LanguagePresentationPolicy.ShouldRightAlign(
                  request.LanguageId));
    }
    catch (Exception ex)
    {
      this.retryAfterByKey[cacheKey] = DateTime.UtcNow + FailureCooldown;
      PluginRuntimeLog.Warning(
          $"Failed to generate RTL text texture for surface '{request.SurfaceId}' and language '{request.LanguageCode}': {ex.Message}");
      return null;
    }
  }

  /// <inheritdoc/>
  public void Dispose()
  {
    this.textureCache.Dispose();
    GC.SuppressFinalize(this);
  }

  /// <summary>
  /// Clears generated texture state.
  /// </summary>
  public void Clear()
  {
    this.textureCache.Clear();
    this.adaptiveHoverTooltipWidthByKey.Clear();
    this.retryAfterByKey.Clear();
  }

  /// <summary>
  /// Gets debug information about the underlying cache.
  /// </summary>
  /// <returns>Current cache count and estimated memory usage.</returns>
  public (int Count, long EstimatedMemoryBytes) GetDebugStats()
  {
    return this.textureCache.GetDebugStats();
  }

  /// <summary>
  /// Resolves the adaptive wrap width used for texture-backed hover tooltips.
  /// Results are cached by text, font, and viewport characteristics so
  /// measurement is paid at most once per unique layout input.
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
    return this.adaptiveHoverTooltipWidthByKey.GetOrAdd(
        cacheKey,
        _ => this.ResolveAdaptiveHoverTooltipMaxWidthCore(
            request,
            viewportWidth));
  }

  private string BuildCacheKey(TextLayoutRequest request)
  {
    var fontPath = this.ResolveFontPath(request);
    var fontSize = this.ResolveFontSize(request).ToString("0.###", CultureInfo.InvariantCulture);
    var maxWidth = this.ResolveMaxWidth(request)?.ToString(CultureInfo.InvariantCulture) ?? "none";
    var lineHeightScale = Math.Clamp(
        this.configuration.TexturePresentationLineHeightScale,
        0.8f,
        1.2f).ToString("0.###", CultureInfo.InvariantCulture);
    var textColor = this.SerializeColor(request.TextColor);
    var backgroundColor = this.SerializeColor(request.BackgroundColor);

    return string.Join(
        "|",
        request.LanguageId.ToString(CultureInfo.InvariantCulture),
        request.LanguageCode,
        request.SurfaceId.ToString(),
        fontPath,
        fontSize,
        maxWidth,
        lineHeightScale,
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
                    1.2f));
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
}
