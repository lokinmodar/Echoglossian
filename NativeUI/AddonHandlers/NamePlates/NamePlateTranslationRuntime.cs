// <copyright file="NamePlateTranslationRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game.Gui.NamePlate;

using Echoglossian.Cache;

namespace Echoglossian.NativeUI.AddonHandlers.NamePlates;

/// <summary>
///     Translates eligible world-object nameplates through Dalamud's
///     <see cref="INamePlateGui" /> surface.
/// </summary>
internal sealed class NamePlateTranslationRuntime : IDisposable
{
  private static readonly TimeSpan FailureCooldown = TimeSpan.FromMinutes(1);
  private static readonly TimeSpan MissingObjectOverlayLifetime =
      TimeSpan.FromSeconds(2);

  private readonly Config config;
  private readonly IGameGui gameGui;
  private readonly Func<NamePlateMessage, Task<string>> insertNamePlateMessageAsync;
  private readonly ConcurrentDictionary<string, byte> inFlightTranslations =
      new(StringComparer.Ordinal);
  private readonly Func<string, string> normalizeReplacementText;
  private readonly IObjectTable objectTable;
  private readonly ConcurrentDictionary<string, DateTime> recentFailures =
      new(StringComparer.Ordinal);
  private readonly ConcurrentDictionary<ulong, NamePlateOverlayEntry> overlays =
      new();
  private readonly TranslationOverlayRenderer renderer;
  private readonly TranslationService translationService;

  private bool disposed;

  /// <summary>
  ///     Initializes a new instance of the <see cref="NamePlateTranslationRuntime" /> class.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <param name="translationService">The shared translation service.</param>
  /// <param name="gameGui">The game UI service used for world-to-screen projection.</param>
  /// <param name="objectTable">The object table used to resolve live object positions.</param>
  /// <param name="renderer">The shared translation overlay renderer.</param>
  /// <param name="insertNamePlateMessageAsync">
  ///     Delegate used to persist a translated nameplate row.
  /// </param>
  /// <param name="normalizeReplacementText">
  ///     Delegate used to normalize translated text before native replacement.
  /// </param>
  public NamePlateTranslationRuntime(
      Config config,
      TranslationService translationService,
      IGameGui gameGui,
      IObjectTable objectTable,
      TranslationOverlayRenderer renderer,
      Func<NamePlateMessage, Task<string>> insertNamePlateMessageAsync,
      Func<string, string> normalizeReplacementText)
  {
    this.config = config;
    this.translationService = translationService;
    this.gameGui = gameGui;
    this.objectTable = objectTable;
    this.renderer = renderer;
    this.insertNamePlateMessageAsync = insertNamePlateMessageAsync;
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
    if (this.disposed ||
        !this.config.Translate ||
        !this.config.TranslateNamePlates ||
        handlers.Count == 0)
    {
      return;
    }

    var effectiveEngine = this.translationService.GetEffectiveTranslationEngineId(
        TranslationSurfaceGroup.Default);
    if (!TranslationReuseScope.TryCreate(
            this.config,
            effectiveEngine,
            out var scope))
    {
      return;
    }

    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      return;
    }

    foreach (var handler in handlers)
    {
      this.ProcessNamePlate(handler, scope, sourceLanguage, effectiveEngine);
    }
  }

  /// <summary>
  ///     Draws active nameplate overlays using the shared renderer and RTL
  ///     presentation stack.
  /// </summary>
  public void DrawOverlays()
  {
    if (this.disposed)
    {
      return;
    }

    if (!this.ShouldUseOverlay())
    {
      this.ClearAllOverlays();
      return;
    }

    var snapshot = this.overlays.ToArray();
    if (snapshot.Length == 0)
    {
      return;
    }

    var viewport = ImGui.GetMainViewport();
    var windowConfig = TranslationWindowConfig.FromConfigForNamePlate(
        this.config);
    foreach (var pair in snapshot)
    {
      var entry = pair.Value;
      if (entry.Overlay.IsDisposed)
      {
        this.overlays.TryRemove(pair.Key, out _);
        continue;
      }

      if (!this.TrySyncOverlayPosition(entry, viewport.Size))
      {
        this.RemoveOverlayIfStale(pair.Key, entry);
        continue;
      }

      entry.Overlay.Semaphore.Wait();
      var shouldDisplay = entry.Overlay.Display;
      entry.Overlay.Semaphore.Release();
      if (!shouldDisplay)
      {
        continue;
      }

      this.renderer.Draw(
          new TranslationOverlayRenderRequest(
              entry.Overlay,
              windowConfig,
              Vector2.Zero,
              viewport.Size,
              entry.Overlay.Position,
              entry.Overlay.Dimensions,
              IsPreview: false));
    }
  }

  /// <inheritdoc />
  public void Dispose()
  {
    this.disposed = true;
    this.ClearAllOverlays(dispose: true);
  }

  private void ProcessNamePlate(
      INamePlateUpdateHandler handler,
      TranslationReuseScope scope,
      SourceClientLanguage sourceLanguage,
      int effectiveEngine)
  {
    var kind = handler.NamePlateKind;
    if (!NamePlateTranslationPolicy.ShouldTranslateKind(kind))
    {
      this.ClearOverlay(handler.GameObjectId);
      return;
    }

    var originalText = ResolveOriginalName(handler);
    if (string.IsNullOrWhiteSpace(originalText))
    {
      this.ClearOverlay(handler.GameObjectId);
      return;
    }

    var worldPosition = handler.GameObject?.Position;
    if (NamePlateCacheManager.TryFindMatch(kind, originalText, scope) is { } cached &&
        !string.IsNullOrWhiteSpace(cached.TranslatedNamePlateText))
    {
      this.ApplyResolvedNamePlate(
          handler,
          kind,
          originalText,
          cached.TranslatedNamePlateText!,
          worldPosition);
      return;
    }

    this.QueueTranslationIfNeeded(
        handler.GameObjectId,
        kind,
        originalText,
        sourceLanguage,
        effectiveEngine,
        worldPosition);
  }

  private void ApplyResolvedNamePlate(
      INamePlateUpdateHandler handler,
      NamePlateKind kind,
      string originalText,
      string translatedText,
      Vector3? worldPosition)
  {
    if (this.ShouldUseOverlay())
    {
      this.UpdateOverlay(
          handler.GameObjectId,
          kind,
          originalText,
          translatedText,
          worldPosition);

      if (!this.ShouldSwapTexts())
      {
        return;
      }
    }
    else
    {
      this.ClearOverlay(handler.GameObjectId);
    }

    if (!this.ShouldApplyNativeText())
    {
      return;
    }

    handler.SetField(
        NamePlateStringField.Name,
        this.NormalizeForNativeReplacement(translatedText));
  }

  private void QueueTranslationIfNeeded(
      ulong gameObjectId,
      NamePlateKind kind,
      string originalText,
      SourceClientLanguage sourceLanguage,
      int effectiveEngine,
      Vector3? worldPosition)
  {
    var translationKey = BuildTranslationKey(
        kind,
        originalText,
        sourceLanguage.PersistenceCode,
        LangDict[LanguageInt].Code,
        effectiveEngine);
    if (this.IsInFailureCooldown(translationKey) ||
        !this.inFlightTranslations.TryAdd(translationKey, 0))
    {
      return;
    }

    Task.Run(() => this.ResolveTranslationAsync(
        translationKey,
        gameObjectId,
        kind,
        originalText,
        sourceLanguage,
        effectiveEngine,
        worldPosition));
  }

  private async Task ResolveTranslationAsync(
      string translationKey,
      ulong gameObjectId,
      NamePlateKind kind,
      string originalText,
      SourceClientLanguage sourceLanguage,
      int effectiveEngine,
      Vector3? worldPosition)
  {
    try
    {
      var originContext = BuildSurfaceIdentity(kind);
      var translatedText = await this.translationService.TranslateAsync(
          originalText,
          sourceLanguage,
          LangDict[LanguageInt].Code,
          TranslationSurfaceGroup.Default,
          originContext: originContext).ConfigureAwait(false);

      if (string.IsNullOrWhiteSpace(translatedText))
      {
        this.recentFailures[translationKey] = DateTime.UtcNow;
        return;
      }

      var row = new NamePlateMessage(
          (int)kind,
          originalText,
          sourceLanguage.PersistenceCode,
          translatedText,
          LangDict[LanguageInt].Code,
          effectiveEngine,
          DateTime.Now,
          DateTime.Now);
      await this.insertNamePlateMessageAsync(row).ConfigureAwait(false);
      NamePlateCacheManager.Update(row);

      if (this.ShouldUseOverlay())
      {
        this.UpdateOverlay(
            gameObjectId,
            kind,
            originalText,
            translatedText,
            worldPosition);
      }

      NamePlateGuiInterface.RequestRedraw();
    }
    catch (Exception ex)
    {
      this.recentFailures[translationKey] = DateTime.UtcNow;
      PluginRuntimeLog.Warning(
          "NamePlateTranslationRuntime",
          $"Failed to translate {BuildSurfaceIdentity(kind)}: {ex.Message}");
    }
    finally
    {
      this.inFlightTranslations.TryRemove(translationKey, out _);
    }
  }

  private bool TrySyncOverlayPosition(
      NamePlateOverlayEntry entry,
      Vector2 viewportSize)
  {
    var gameObject = this.objectTable.SearchById(entry.GameObjectId);
    if (gameObject is { } liveObject && liveObject.IsValid())
    {
      var verticalOffset = Math.Max(1.5f, liveObject.HitboxRadius * 1.4f);
      entry.WorldPosition = liveObject.Position + new Vector3(0f, verticalOffset, 0f);
      entry.LastObjectSeenAt = DateTime.UtcNow;
    }

    if (entry.WorldPosition == null)
    {
      return false;
    }

    if (!this.gameGui.WorldToScreen(
            entry.WorldPosition.Value,
            out var screenPosition,
            out var inView) ||
        !inView)
    {
      return false;
    }

    var width = Math.Clamp(viewportSize.X * 0.16f, 180f, 420f);
    entry.Overlay.Position = screenPosition;
    entry.Overlay.Dimensions = new Vector2(width, 32f);
    return true;
  }

  private void UpdateOverlay(
      ulong gameObjectId,
      NamePlateKind kind,
      string originalText,
      string translatedText,
      Vector3? worldPosition)
  {
    var overlayText = this.SelectOverlayText(originalText, translatedText);
    if (string.IsNullOrWhiteSpace(overlayText))
    {
      this.ClearOverlay(gameObjectId);
      return;
    }

    var entry = this.overlays.GetOrAdd(
        gameObjectId,
        id => new NamePlateOverlayEntry(id, new TranslationOverlay()));
    entry.Kind = kind;
    entry.WorldPosition = worldPosition ?? entry.WorldPosition;
    entry.LastObjectSeenAt = DateTime.UtcNow;

    entry.Overlay.NameSemaphore.Wait();
    try
    {
      entry.Overlay.OriginalName = BuildSurfaceIdentity(kind);
      entry.Overlay.CurrentName = string.Empty;
      entry.Overlay.CurrentNameId++;
    }
    finally
    {
      entry.Overlay.NameSemaphore.Release();
    }

    entry.Overlay.Semaphore.Wait();
    try
    {
      entry.Overlay.CurrentText =
          TranslationOverlayTextNormalizationHelper.NormalizeForDisplay(
              overlayText);
      entry.Overlay.Display = true;
      entry.Overlay.CurrentTextId++;
    }
    finally
    {
      entry.Overlay.Semaphore.Release();
    }
  }

  private void ClearOverlay(ulong gameObjectId)
  {
    if (!this.overlays.TryRemove(gameObjectId, out var entry))
    {
      return;
    }

    entry.Overlay.Dispose();
  }

  private void ClearAllOverlays(bool dispose = false)
  {
    foreach (var pair in this.overlays.ToArray())
    {
      if (!this.overlays.TryRemove(pair.Key, out var entry))
      {
        continue;
      }

      if (dispose || !entry.Overlay.IsDisposed)
      {
        entry.Overlay.Dispose();
      }
    }
  }

  private void RemoveOverlayIfStale(
      ulong gameObjectId,
      NamePlateOverlayEntry entry)
  {
    if (DateTime.UtcNow - entry.LastObjectSeenAt < MissingObjectOverlayLifetime)
    {
      return;
    }

    this.ClearOverlay(gameObjectId);
  }

  private bool IsInFailureCooldown(string translationKey)
  {
    if (!this.recentFailures.TryGetValue(translationKey, out var failedAt))
    {
      return false;
    }

    if (DateTime.UtcNow - failedAt < FailureCooldown)
    {
      return true;
    }

    this.recentFailures.TryRemove(translationKey, out _);
    return false;
  }

  private bool ShouldUseOverlay()
  {
    return this.config.Translate &&
           this.config.TranslateNamePlates &&
           TranslationDisplayModeHelper.UsesOverlayPresentation(
               this.config.NamePlateTranslationDisplayMode,
               this.config.OverlayOnlyLanguage);
  }

  private bool ShouldApplyNativeText()
  {
    return this.config.Translate &&
           this.config.TranslateNamePlates &&
           TranslationDisplayModeHelper.WritesNativeTranslation(
               this.config.NamePlateTranslationDisplayMode,
               this.config.OverlayOnlyLanguage);
  }

  private bool ShouldSwapTexts()
  {
    return this.config.Translate &&
           this.config.TranslateNamePlates &&
           TranslationDisplayModeHelper.ShowsOriginalOverlayText(
               this.config.NamePlateTranslationDisplayMode,
               this.config.OverlayOnlyLanguage);
  }

  private string SelectOverlayText(
      string originalText,
      string translatedText)
  {
    if (this.ShouldSwapTexts() &&
        !string.IsNullOrWhiteSpace(originalText))
    {
      return originalText;
    }

    return translatedText;
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

  private static string BuildSurfaceIdentity(NamePlateKind kind)
  {
    return $"NamePlate/{kind}";
  }

  private static string BuildTranslationKey(
      NamePlateKind kind,
      string originalText,
      string sourceLanguage,
      string targetLanguage,
      int engine)
  {
    return $"{(int)kind}|{sourceLanguage}|{targetLanguage}|{engine}|{originalText}";
  }

  private sealed class NamePlateOverlayEntry
  {
    internal NamePlateOverlayEntry(
        ulong gameObjectId,
        TranslationOverlay overlay)
    {
      this.GameObjectId = gameObjectId;
      this.Overlay = overlay;
      this.LastObjectSeenAt = DateTime.UtcNow;
    }

    internal ulong GameObjectId { get; }

    internal NamePlateKind Kind { get; set; }

    internal TranslationOverlay Overlay { get; }

    internal DateTime LastObjectSeenAt { get; set; }

    internal Vector3? WorldPosition { get; set; }
  }
}
