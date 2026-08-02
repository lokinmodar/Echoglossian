// <copyright file="NamePlateTranslationRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game.Gui.NamePlate;

using Echoglossian.Cache;

using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace Echoglossian.NativeUI.AddonHandlers.NamePlates;

/// <summary>
///     Translates eligible world-object nameplates through Dalamud's
///     <see cref="INamePlateGui" /> surface.
/// </summary>
internal sealed class NamePlateTranslationRuntime : IDisposable
{
  private const float MinimumNamePlateVerticalOffset = 0.75f;
  private const float NamePlateVerticalOffsetPadding = 0.25f;
  private const float NamePlateOverlayAnchorHeight = 24f;

  private readonly Config config;
  private readonly TranslationOverlay distanceAwareOverlay;
  private readonly object distanceAwareOverlayGate = new();
  private readonly IGameGui gameGui;
  private readonly Func<string, string> normalizeReplacementText;
  private readonly ReentrantCallbackGuard namePlateUpdateGuard = new();
  private readonly Action<NamePlatePrefetchCandidate> trackPrefetchCandidate;
  private readonly TranslationService translationService;

  private NamePlateDistanceAwareOverlayFrame? distanceAwareOverlayFrame;
  private bool disposed;

  /// <summary>
  ///     Initializes a new instance of the <see cref="NamePlateTranslationRuntime" /> class.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The shared translation service.</param>
  /// <param name="gameGui">The game GUI service used for world projection.</param>
  /// <param name="distanceAwareOverlay">
  ///     The shared overlay used for the overlay-only NamePlate backend.
  /// </param>
  /// <param name="trackPrefetchCandidate">
  ///     Delegate used to record stable nameplate source text for background
  ///     prefetch outside the one-frame handler callback.
  /// </param>
  /// <param name="normalizeReplacementText">
  ///     Delegate used to normalize translated text before native replacement.
  /// </param>
  public NamePlateTranslationRuntime(
      Config config,
      TranslationService translationService,
      IGameGui gameGui,
      TranslationOverlay distanceAwareOverlay,
      Action<NamePlatePrefetchCandidate> trackPrefetchCandidate,
      Func<string, string> normalizeReplacementText)
  {
    this.config = config;
    this.translationService = translationService;
    this.gameGui = gameGui;
    this.distanceAwareOverlay = distanceAwareOverlay;
    this.trackPrefetchCandidate = trackPrefetchCandidate;
    this.normalizeReplacementText = normalizeReplacementText;
  }

  /// <summary>
  ///     Handles nameplate updates from Dalamud. Handlers are used only inside
  ///     this callback because Dalamud marks them valid for one frame.
  /// </summary>
  /// <param name="context">The current nameplate update context.</param>
  /// <param name="handlers">The updated nameplate handlers.</param>
  public void HandleNamePlateUpdate(
      INamePlateUpdateContext context,
      IReadOnlyList<INamePlateUpdateHandler> handlers)
  {
    var callbackLease = this.namePlateUpdateGuard.TryEnter();
    if (callbackLease == null)
    {
      return;
    }

    using (callbackLease)
    {
      if (this.disposed ||
          !FrameworkAccessGuard.IsClientReadyForPlayerScopedFrameworkAccess() ||
          !this.config.Translate ||
          !this.config.TranslateNamePlates ||
          handlers.Count == 0)
      {
        return;
      }

      var effectiveEngine =
          this.translationService.GetEffectiveTranslationEngineId(
              TranslationSurfaceGroup.Default);
      if (!TranslationReuseScope.TryCreate(
              this.config,
              effectiveEngine,
              out var scope))
      {
        return;
      }

      foreach (var handler in handlers)
      {
        this.ProcessNamePlate(handler, scope);
      }
    }
  }

  /// <inheritdoc />
  public void Dispose()
  {
    this.disposed = true;
  }

  /// <summary>
  ///     Resolves the world-space anchor used to project a nameplate overlay.
  /// </summary>
  /// <param name="objectPosition">The live object world position.</param>
  /// <param name="hitboxRadius">The live object hitbox radius.</param>
  /// <returns>The world-space anchor projected for overlay placement.</returns>
  internal static Vector3 ResolveNamePlateWorldAnchor(
      Vector3 objectPosition,
      float hitboxRadius)
  {
    var verticalOffset = Math.Max(
        MinimumNamePlateVerticalOffset,
        Math.Max(0f, hitboxRadius) + NamePlateVerticalOffsetPadding);
    return objectPosition + new Vector3(0f, verticalOffset, 0f);
  }

  /// <summary>
  ///     Resolves the screen-space overlay bounds from the projected nameplate
  ///     center point.
  /// </summary>
  /// <param name="screenPosition">The projected nameplate center point.</param>
  /// <param name="viewportSize">The current viewport size.</param>
  /// <returns>The overlay top-left position and initial size.</returns>
  internal static (Vector2 Position, Vector2 Size) ResolveCenteredNamePlateOverlayBounds(
      Vector2 screenPosition,
      Vector2 viewportSize)
  {
    var width = Math.Clamp(viewportSize.X * 0.16f, 180f, 420f);
    var size = new Vector2(width, NamePlateOverlayAnchorHeight);
    return (screenPosition - (size * 0.5f), size);
  }

  /// <summary>
  ///     Synchronizes the latest projected NamePlate frame to the overlay
  ///     bounds used by the shared UI-thread renderer.
  /// </summary>
  /// <param name="overlay">The NamePlate overlay to synchronize.</param>
  /// <param name="viewportSize">The active viewport size.</param>
  /// <returns>Whether a visible NamePlate frame was synchronized.</returns>
  internal bool TrySyncDistanceAwareOverlayFrame(
      TranslationOverlay overlay,
      Vector2 viewportSize)
  {
    NamePlateDistanceAwareOverlayFrame? frame;
    lock (this.distanceAwareOverlayGate)
    {
      frame = this.distanceAwareOverlayFrame;
    }

    if (frame == null || !overlay.Display)
    {
      return false;
    }

    var bounds = ResolveCenteredNamePlateOverlayBounds(
        frame.Value.ScreenPosition,
        viewportSize);
    overlay.Position = bounds.Position;
    overlay.Dimensions = bounds.Size;
    overlay.UpdateRuntimePresentation(
        frame.Value.ScaleMultiplier,
        frame.Value.AlphaMultiplier);
    return true;
  }

  private void ProcessNamePlate(
      INamePlateUpdateHandler handler,
      TranslationReuseScope scope)
  {
    var kind = handler.NamePlateKind;
    if (!NamePlateTranslationPolicy.ShouldTranslateKind(kind))
    {
      return;
    }

    var originalText = ResolveOriginalName(handler);
    if (string.IsNullOrWhiteSpace(originalText))
    {
      return;
    }

    if (NamePlateCacheManager.TryFindMatch(kind, originalText, scope) is { } cached &&
        !string.IsNullOrWhiteSpace(cached.TranslatedNamePlateText))
    {
      this.ApplyResolvedNamePlate(
          handler,
          originalText,
          cached.TranslatedNamePlateText!);
      return;
    }

    this.trackPrefetchCandidate(new NamePlatePrefetchCandidate(kind, originalText));
  }

  private void ApplyResolvedNamePlate(
      INamePlateUpdateHandler handler,
      string originalText,
      string translatedText)
  {
    if (this.ShouldSuppressTranslatedPresentation())
    {
      this.ClearDistanceAwareOverlay();
      return;
    }

    if (this.ShouldUseDistanceAwareOverlayBackend())
    {
      this.ApplyDistanceAwareOverlay(handler, originalText, translatedText);
      return;
    }

    this.ClearDistanceAwareOverlay();
    var presentationPlan = NamePlateNativePresentationPlan.Create(
        originalText,
        translatedText,
        this.config.NamePlateTranslationDisplayMode,
        this.config.OverlayOnlyLanguage);

    if (presentationPlan.ShowsTitle &&
        !string.IsNullOrWhiteSpace(presentationPlan.TitleText))
    {
      handler.DisplayTitle = true;
      handler.IsPrefixTitle = true;
      handler.SetField(
          NamePlateStringField.Title,
          presentationPlan.TitleText!);
    }

    if (!presentationPlan.WritesTranslatedName ||
        string.IsNullOrWhiteSpace(presentationPlan.NameText))
    {
      return;
    }

    handler.SetField(
        NamePlateStringField.Name,
        this.NormalizeForNativeReplacement(presentationPlan.NameText!));
  }

  private void ApplyDistanceAwareOverlay(
      INamePlateUpdateHandler handler,
      string originalText,
      string translatedText)
  {
    var gameObject = handler.GameObject;
    if (gameObject == null)
    {
      this.ClearDistanceAwareOverlay();
      return;
    }

    var worldAnchor = ResolveNamePlateWorldAnchor(
        gameObject.Position,
        gameObject.HitboxRadius);
    if (!this.gameGui.WorldToScreen(worldAnchor, out var screenPosition, out var inView) ||
        !inView)
    {
      this.ClearDistanceAwareOverlay();
      return;
    }

    var distanceToCamera = this.ResolveDistanceToCamera(worldAnchor);
    var presentation = DistanceAwareOverlayPresentation.Resolve(
        distanceToCamera,
        this.config.DistanceAwareOverlayFullScaleDistance,
        this.config.DistanceAwareOverlayFadeStartDistance,
        this.config.DistanceAwareOverlayMaxDistance,
        this.config.DistanceAwareOverlayMinScale);
    if (!presentation.IsVisible)
    {
      this.ClearDistanceAwareOverlay();
      return;
    }

    lock (this.distanceAwareOverlayGate)
    {
      this.distanceAwareOverlayFrame = new NamePlateDistanceAwareOverlayFrame(
          screenPosition,
          presentation.Scale,
          presentation.Alpha);
    }

    this.distanceAwareOverlay.UpdateRuntimePresentation(
        presentation.Scale,
        presentation.Alpha);
    this.distanceAwareOverlay.CurrentName = string.Empty;
    this.distanceAwareOverlay.OriginalName = originalText;
    this.distanceAwareOverlay.CurrentText =
        TranslationOverlayTextNormalizationHelper.NormalizeForDisplay(
            translatedText);
    this.distanceAwareOverlay.Display = true;
  }

  private void ClearDistanceAwareOverlay()
  {
    lock (this.distanceAwareOverlayGate)
    {
      this.distanceAwareOverlayFrame = null;
    }

    this.distanceAwareOverlay.Display = false;
    this.distanceAwareOverlay.CurrentText = string.Empty;
    this.distanceAwareOverlay.CurrentName = string.Empty;
    this.distanceAwareOverlay.OriginalName = string.Empty;
    this.distanceAwareOverlay.ClearRuntimePresentation();
  }

  private unsafe float ResolveDistanceToCamera(Vector3 worldAnchor)
  {
    var cameraManager = CameraManager.Instance();
    if (cameraManager == null)
    {
      return float.MaxValue;
    }

    var camera = cameraManager->Cameras[0].Value;
    if (camera == null || camera->CameraBase.SceneCamera.RenderCamera == null)
    {
      return float.MaxValue;
    }

    var cameraOrigin = camera->CameraBase.SceneCamera.RenderCamera->Origin;
    return Vector3.Distance(
        worldAnchor,
        new Vector3(cameraOrigin.X, cameraOrigin.Y, cameraOrigin.Z));
  }

  private bool ShouldSuppressTranslatedPresentation()
  {
    return this.config.OverlayOnlyLanguage &&
           !this.config.EnableDistanceAwareOverlays;
  }

  private bool ShouldUseDistanceAwareOverlayBackend()
  {
    return this.config.OverlayOnlyLanguage &&
           this.config.EnableDistanceAwareOverlays;
  }

  private string NormalizeForNativeReplacement(string translatedText)
  {
    return this.config.RemoveDiacriticsWhenUsingReplacementTalkBTalk
        ? this.normalizeReplacementText(translatedText)
        : translatedText;
  }

  private static string ResolveOriginalName(INamePlateUpdateHandler handler)
  {
    var infoName = handler.InfoView.Name.TextValue;
    if (!string.IsNullOrWhiteSpace(infoName))
    {
      return infoName.Trim();
    }

    return handler.GetFieldAsString(NamePlateStringField.Name).Trim();
  }
}
