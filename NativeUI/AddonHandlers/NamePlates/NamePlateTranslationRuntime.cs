// <copyright file="NamePlateTranslationRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game.Gui.NamePlate;

using Echoglossian.Cache;
using Echoglossian.NativeUI.Helpers;

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
  private readonly NamePlateDistanceAwareOverlayLifecycle distanceAwareOverlayLifecycle = new();
  private readonly IGameGui gameGui;
  private readonly Func<string, string> normalizeReplacementText;
  private readonly ReentrantCallbackGuard namePlateUpdateGuard = new();
  private readonly IObjectTable objectTable;
  private readonly Action<NamePlatePrefetchCandidate> trackPrefetchCandidate;
  private readonly TranslationService translationService;

  private bool disposed;

  /// <summary>
  ///     Initializes a new instance of the <see cref="NamePlateTranslationRuntime" /> class.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The shared translation service.</param>
  /// <param name="gameGui">The game GUI service used for world projection.</param>
  /// <param name="objectTable">The object table used to resolve retained entities.</param>
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
      IObjectTable objectTable,
      TranslationOverlay distanceAwareOverlay,
      Action<NamePlatePrefetchCandidate> trackPrefetchCandidate,
      Func<string, string> normalizeReplacementText)
  {
    this.config = config;
    this.translationService = translationService;
    this.gameGui = gameGui;
    this.objectTable = objectTable;
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
      if (this.ShouldUseDistanceAwareOverlayBackend())
      {
        this.distanceAwareOverlayLifecycle.BeginNamePlateUpdate(
            context.IsFullUpdate,
            context.ActiveNamePlateCount);
      }
      else
      {
        this.ClearDistanceAwareOverlay();
      }

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
    this.ClearDistanceAwareOverlay();
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
  ///     Resolves retained candidates from current object, projection, and
  ///     camera state before synchronizing the shared overlay.
  /// </summary>
  /// <param name="overlay">The NamePlate overlay to synchronize.</param>
  /// <param name="viewportSize">The active viewport size.</param>
  /// <returns>Whether a visible NamePlate frame was synchronized.</returns>
  internal bool TrySyncDistanceAwareOverlayFrame(
      TranslationOverlay overlay,
      Vector2 viewportSize)
  {
    if (this.disposed || !this.ShouldUseDistanceAwareOverlayBackend())
    {
      this.ClearDistanceAwareOverlay(
          this.disposed
              ? "sync-skipped-disposed"
              : "sync-skipped-backend-disabled");
      return false;
    }

    var synchronized = this.distanceAwareOverlayLifecycle.TrySync(
        overlay,
        viewportSize,
        this.ResolveLiveDistanceAwareOverlayFrame);
    OverlayPublicationDiagnostics.Log(
        "NamePlateOverlayDiag",
        synchronized ? "sync-visible" : "sync-not-visible",
        $"{synchronized}|{MathF.Round(viewportSize.X / 32f)},{MathF.Round(viewportSize.Y / 32f)}",
        string.Create(
            CultureInfo.InvariantCulture,
            $"{synchronized}|{MathF.Round(viewportSize.X / 32f)},{MathF.Round(viewportSize.Y / 32f)}|" +
            $"{overlay.Display}|{OverlayPublicationDiagnostics.BuildPreview(overlay.CurrentText)}"),
        string.Create(
            CultureInfo.InvariantCulture,
            $"viewport={OverlayPublicationDiagnostics.FormatVector(viewportSize)} overlayDisplay={overlay.Display} " +
            $"textLen={overlay.CurrentText.Length} preview='{OverlayPublicationDiagnostics.BuildPreview(overlay.CurrentText)}'"));
    return synchronized;
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
      OverlayPublicationDiagnostics.Log(
          "NamePlateOverlayDiag",
          "cache-hit",
          $"{kind}|{OverlayPublicationDiagnostics.BuildPreview(originalText)}",
          string.Create(
              CultureInfo.InvariantCulture,
              $"{kind}|{OverlayPublicationDiagnostics.BuildPreview(originalText)}|" +
              $"{OverlayPublicationDiagnostics.BuildPreview(cached.TranslatedNamePlateText)}"),
          string.Create(
              CultureInfo.InvariantCulture,
              $"kind={kind} originalLen={originalText.Length} " +
              $"originalPreview='{OverlayPublicationDiagnostics.BuildPreview(originalText)}' " +
              $"translatedLen={cached.TranslatedNamePlateText.Length} " +
              $"translatedPreview='{OverlayPublicationDiagnostics.BuildPreview(cached.TranslatedNamePlateText)}'"));
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
      OverlayPublicationDiagnostics.Log(
          "NamePlateOverlayDiag",
          "presentation-suppressed",
          OverlayPublicationDiagnostics.BuildPreview(originalText),
          string.Create(
              CultureInfo.InvariantCulture,
              $"{OverlayPublicationDiagnostics.BuildPreview(originalText)}|" +
              $"{this.config.OverlayOnlyLanguage}|{this.config.EnableDistanceAwareOverlays}"),
          string.Create(
              CultureInfo.InvariantCulture,
              $"overlayOnlyLanguage={this.config.OverlayOnlyLanguage} " +
              $"distanceAwareEnabled={this.config.EnableDistanceAwareOverlays} " +
              $"originalPreview='{OverlayPublicationDiagnostics.BuildPreview(originalText)}' " +
              $"translatedPreview='{OverlayPublicationDiagnostics.BuildPreview(translatedText)}'"));
      this.ClearDistanceAwareOverlay("presentation-suppressed");
      return;
    }

    if (this.ShouldUseDistanceAwareOverlayBackend())
    {
      this.RetainDistanceAwareOverlayCandidate(
          handler,
          originalText,
          translatedText);
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

  private void RetainDistanceAwareOverlayCandidate(
      INamePlateUpdateHandler handler,
      string originalText,
      string translatedText)
  {
    var gameObjectId = handler.GameObjectId;
    if (gameObjectId == 0ul)
    {
      return;
    }

    var gameObject = handler.GameObject;
    var entityId = gameObject?.EntityId ?? 0u;

    OverlayPublicationDiagnostics.Log(
        "NamePlateOverlayDiag",
        "retain-candidate",
        $"{gameObjectId}|{OverlayPublicationDiagnostics.BuildPreview(originalText)}",
        string.Create(
            CultureInfo.InvariantCulture,
            $"{gameObjectId}|{OverlayPublicationDiagnostics.BuildPreview(originalText)}|" +
            $"{OverlayPublicationDiagnostics.BuildPreview(translatedText)}"),
        string.Create(
            CultureInfo.InvariantCulture,
            $"gameObjectId={gameObjectId} entityId={entityId} originalLen={originalText.Length} " +
            $"originalPreview='{OverlayPublicationDiagnostics.BuildPreview(originalText)}' " +
            $"translatedLen={translatedText.Length} translatedPreview='{OverlayPublicationDiagnostics.BuildPreview(translatedText)}'"));
    this.distanceAwareOverlayLifecycle.UpsertCandidate(
        new NamePlateDistanceAwareOverlayCandidate(
            gameObjectId,
            originalText,
            TranslationOverlayTextNormalizationHelper.NormalizeForDisplay(
                translatedText),
            entityId));
  }

  private NamePlateDistanceAwareOverlayFrame? ResolveLiveDistanceAwareOverlayFrame(
      NamePlateDistanceAwareOverlayCandidate candidate)
  {
    var gameObject = this.objectTable.SearchById(candidate.GameObjectId);
    if (gameObject == null)
    {
      OverlayPublicationDiagnostics.Log(
          "NamePlateOverlayDiag",
          "resolve-frame-miss",
          $"{candidate.GameObjectId}|missing-object",
          $"{candidate.GameObjectId}|missing-object",
          string.Create(
              CultureInfo.InvariantCulture,
              $"gameObjectId={candidate.GameObjectId} entityId={candidate.EntityId} " +
              $"reason=missing-object originalPreview='{OverlayPublicationDiagnostics.BuildPreview(candidate.OriginalText)}'"));
      return null;
    }

    var worldAnchor = ResolveNamePlateWorldAnchor(
        gameObject.Position,
        gameObject.HitboxRadius);
    if (!this.gameGui.WorldToScreen(worldAnchor, out var screenPosition, out var inView) ||
        !inView)
    {
      OverlayPublicationDiagnostics.Log(
          "NamePlateOverlayDiag",
          "resolve-frame-miss",
          $"{candidate.GameObjectId}|projection",
          string.Create(
              CultureInfo.InvariantCulture,
              $"{candidate.GameObjectId}|projection|{inView}|" +
              $"{MathF.Round(worldAnchor.X):0},{MathF.Round(worldAnchor.Y):0},{MathF.Round(worldAnchor.Z):0}"),
          string.Create(
              CultureInfo.InvariantCulture,
              $"gameObjectId={candidate.GameObjectId} entityId={candidate.EntityId} " +
              $"reason={(inView ? "world-to-screen-failed" : "not-in-view")} " +
              $"worldAnchor=({worldAnchor.X:0.0},{worldAnchor.Y:0.0},{worldAnchor.Z:0.0}) " +
              $"originalPreview='{OverlayPublicationDiagnostics.BuildPreview(candidate.OriginalText)}'"));
      return null;
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
      OverlayPublicationDiagnostics.Log(
          "NamePlateOverlayDiag",
          "resolve-frame-miss",
          $"{candidate.GameObjectId}|distance",
          string.Create(
              CultureInfo.InvariantCulture,
              $"{candidate.GameObjectId}|distance|{distanceToCamera:0.##}|{presentation.Scale:0.##}|{presentation.Alpha:0.##}"),
          string.Create(
              CultureInfo.InvariantCulture,
              $"gameObjectId={candidate.GameObjectId} entityId={candidate.EntityId} " +
              $"reason=distance-clipped distance={distanceToCamera:0.##} " +
              $"scale={presentation.Scale:0.##} alpha={presentation.Alpha:0.##} " +
              $"screen={OverlayPublicationDiagnostics.FormatVector(screenPosition)} " +
              $"originalPreview='{OverlayPublicationDiagnostics.BuildPreview(candidate.OriginalText)}'"));
      return null;
    }

    OverlayPublicationDiagnostics.Log(
        "NamePlateOverlayDiag",
        "resolve-frame-hit",
        $"{candidate.GameObjectId}|{OverlayPublicationDiagnostics.BuildPreview(candidate.OriginalText)}",
        string.Create(
            CultureInfo.InvariantCulture,
            $"{candidate.GameObjectId}|{OverlayPublicationDiagnostics.BuildPreview(candidate.OriginalText)}|" +
            $"{MathF.Round(distanceToCamera):0}|{presentation.Scale:0.##}|{presentation.Alpha:0.##}|" +
            $"{OverlayPublicationDiagnostics.RoundVector(screenPosition).X:0},{OverlayPublicationDiagnostics.RoundVector(screenPosition).Y:0}"),
        string.Create(
            CultureInfo.InvariantCulture,
            $"gameObjectId={candidate.GameObjectId} entityId={candidate.EntityId} " +
            $"distance={distanceToCamera:0.##} " +
            $"screen={OverlayPublicationDiagnostics.FormatVector(screenPosition)} " +
            $"scale={presentation.Scale:0.##} alpha={presentation.Alpha:0.##} " +
            $"originalPreview='{OverlayPublicationDiagnostics.BuildPreview(candidate.OriginalText)}' " +
            $"translatedPreview='{OverlayPublicationDiagnostics.BuildPreview(candidate.TranslatedText)}'"));
    return new NamePlateDistanceAwareOverlayFrame(
        screenPosition,
        distanceToCamera,
        presentation.Scale,
        presentation.Alpha);
  }

  private void ClearDistanceAwareOverlay(string reason = "unspecified")
  {
    this.distanceAwareOverlayLifecycle.ClearCandidates();
    NamePlateDistanceAwareOverlayLifecycle.ClearOverlay(
        this.distanceAwareOverlay,
        reason);
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
