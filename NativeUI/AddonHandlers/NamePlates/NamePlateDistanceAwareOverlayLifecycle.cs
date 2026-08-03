// <copyright file="NamePlateDistanceAwareOverlayLifecycle.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

namespace Echoglossian.NativeUI.AddonHandlers.NamePlates;

/// <summary>
///     Retains stable NamePlate translation candidates and publishes the nearest
///     candidate that resolves to a visible live frame.
/// </summary>
internal sealed class NamePlateDistanceAwareOverlayLifecycle
{
  private readonly Dictionary<ulong, NamePlateDistanceAwareOverlayCandidate> candidates = new();
  private readonly object gate = new();

  /// <summary>
  ///     Updates retained candidate membership for a Dalamud NamePlate update.
  /// </summary>
  /// <param name="isFullUpdate">
  ///     <see langword="true" /> when the callback contains all active nameplates;
  ///     otherwise, <see langword="false" />.
  /// </param>
  /// <param name="activeNamePlateCount">The number of active NamePlate entries.</param>
  internal void BeginNamePlateUpdate(
      bool isFullUpdate,
      int activeNamePlateCount)
  {
    if (!isFullUpdate && activeNamePlateCount > 0)
    {
      return;
    }

    lock (this.gate)
    {
      this.candidates.Clear();
    }
  }

  /// <summary>
  ///     Adds or replaces a retained translation candidate by game object
  ///     identifier.
  /// </summary>
  /// <param name="candidate">The stable translation candidate.</param>
  internal void UpsertCandidate(NamePlateDistanceAwareOverlayCandidate candidate)
  {
    lock (this.gate)
    {
      this.candidates[candidate.GameObjectId] = candidate;
    }
  }

  /// <summary>
  ///     Clears all retained translation candidates.
  /// </summary>
  internal void ClearCandidates()
  {
    lock (this.gate)
    {
      this.candidates.Clear();
    }
  }

  /// <summary>
  ///     Resolves all retained candidates against live frame state and publishes
  ///     the nearest eligible candidate to the shared overlay.
  /// </summary>
  /// <param name="overlay">The shared NamePlate overlay.</param>
  /// <param name="viewportSize">The active viewport size.</param>
  /// <param name="resolveLiveFrame">
  ///     The callback that resolves current projection and distance state.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> if a live candidate was published; otherwise,
  ///     <see langword="false" />.
  /// </returns>
  internal bool TrySync(
      TranslationOverlay overlay,
      Vector2 viewportSize,
      Func<NamePlateDistanceAwareOverlayCandidate, NamePlateDistanceAwareOverlayFrame?> resolveLiveFrame)
  {
    NamePlateDistanceAwareOverlayCandidate[] snapshot;
    lock (this.gate)
    {
      snapshot = this.candidates.Values.ToArray();
    }

    OverlayPublicationDiagnostics.Log(
        "NamePlateOverlayDiag",
        "sync-snapshot",
        $"count={snapshot.Length}",
        $"count={snapshot.Length}",
        string.Create(
            CultureInfo.InvariantCulture,
            $"candidateCount={snapshot.Length} viewport={OverlayPublicationDiagnostics.FormatVector(viewportSize)}"));
    NamePlateDistanceAwareOverlayCandidate? selectedCandidate = null;
    NamePlateDistanceAwareOverlayFrame? selectedFrame = null;
    foreach (var candidate in snapshot)
    {
      var frame = resolveLiveFrame(candidate);
      if (frame == null ||
          (selectedFrame != null &&
           (frame.Value.DistanceToCamera > selectedFrame.Value.DistanceToCamera ||
            (frame.Value.DistanceToCamera == selectedFrame.Value.DistanceToCamera &&
             candidate.GameObjectId >= selectedCandidate!.Value.GameObjectId))))
      {
        continue;
      }

      selectedCandidate = candidate;
      selectedFrame = frame;
    }

    if (selectedCandidate == null || selectedFrame == null)
    {
      OverlayPublicationDiagnostics.Log(
          "NamePlateOverlayDiag",
          "sync-clear",
          $"count={snapshot.Length}",
          $"count={snapshot.Length}|clear",
          string.Create(
              CultureInfo.InvariantCulture,
              $"candidateCount={snapshot.Length} reason=no-visible-candidate overlayDisplay={overlay.Display}"));
      ClearOverlay(overlay, "no-visible-candidate");
      return false;
    }

    var bounds = NamePlateTranslationRuntime.ResolveCenteredNamePlateOverlayBounds(
        selectedFrame.Value.ScreenPosition,
        viewportSize);
    overlay.Position = bounds.Position;
    overlay.Dimensions = bounds.Size;
    overlay.UpdateRuntimePresentation(
        selectedFrame.Value.ScaleMultiplier,
        selectedFrame.Value.AlphaMultiplier);
    overlay.CurrentName = string.Empty;
    overlay.OriginalName = selectedCandidate.Value.OriginalText;
    overlay.CurrentText = selectedCandidate.Value.TranslatedText;
    overlay.Display = true;
    OverlayPublicationDiagnostics.Log(
        "NamePlateOverlayDiag",
        "sync-selected",
        $"{selectedCandidate.Value.GameObjectId}|{OverlayPublicationDiagnostics.BuildPreview(selectedCandidate.Value.OriginalText)}",
        string.Create(
            CultureInfo.InvariantCulture,
            $"{selectedCandidate.Value.GameObjectId}|{OverlayPublicationDiagnostics.BuildPreview(selectedCandidate.Value.OriginalText)}|" +
            $"{MathF.Round(selectedFrame.Value.DistanceToCamera):0}|{selectedFrame.Value.ScaleMultiplier:0.##}|" +
            $"{selectedFrame.Value.AlphaMultiplier:0.##}|" +
            $"{OverlayPublicationDiagnostics.RoundVector(bounds.Position).X:0},{OverlayPublicationDiagnostics.RoundVector(bounds.Position).Y:0}"),
        string.Create(
            CultureInfo.InvariantCulture,
            $"gameObjectId={selectedCandidate.Value.GameObjectId} entityId={selectedCandidate.Value.EntityId} " +
            $"overlayPos={OverlayPublicationDiagnostics.FormatVector(bounds.Position)} " +
            $"overlaySize={OverlayPublicationDiagnostics.FormatVector(bounds.Size)} " +
            $"screen={OverlayPublicationDiagnostics.FormatVector(selectedFrame.Value.ScreenPosition)} " +
            $"distance={selectedFrame.Value.DistanceToCamera:0.##} scale={selectedFrame.Value.ScaleMultiplier:0.##} " +
            $"alpha={selectedFrame.Value.AlphaMultiplier:0.##} " +
            $"originalPreview='{OverlayPublicationDiagnostics.BuildPreview(selectedCandidate.Value.OriginalText)}' " +
            $"translatedPreview='{OverlayPublicationDiagnostics.BuildPreview(selectedCandidate.Value.TranslatedText)}'"));
    return true;
  }

  /// <summary>
  ///     Clears all content and runtime presentation state from an overlay.
  /// </summary>
  /// <param name="overlay">The overlay to clear.</param>
  internal static void ClearOverlay(
      TranslationOverlay overlay,
      string reason = "unspecified")
  {
    OverlayPublicationDiagnostics.Log(
        "NamePlateOverlayDiag",
        "overlay-clear",
        reason,
        string.Create(
            CultureInfo.InvariantCulture,
            $"{reason}|{overlay.Display}|{OverlayPublicationDiagnostics.BuildPreview(overlay.CurrentText)}"),
        string.Create(
            CultureInfo.InvariantCulture,
            $"reason={reason} overlayDisplay={overlay.Display} " +
            $"textLen={overlay.CurrentText.Length} preview='{OverlayPublicationDiagnostics.BuildPreview(overlay.CurrentText)}'"));
    overlay.Display = false;
    overlay.CurrentText = string.Empty;
    overlay.CurrentName = string.Empty;
    overlay.OriginalName = string.Empty;
    overlay.ClearRuntimePresentation();
  }
}

/// <summary>
///     Represents stable translated NamePlate data retained across UI frames.
/// </summary>
/// <param name="GameObjectId">The live object's stable game object identifier.</param>
/// <param name="OriginalText">The original NamePlate text.</param>
/// <param name="TranslatedText">The normalized translated NamePlate text.</param>
/// <param name="EntityId">The live object's entity identifier, when available.</param>
internal readonly record struct NamePlateDistanceAwareOverlayCandidate(
    ulong GameObjectId,
    string OriginalText,
    string TranslatedText,
    uint EntityId = 0u);
