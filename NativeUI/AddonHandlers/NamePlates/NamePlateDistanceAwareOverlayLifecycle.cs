// <copyright file="NamePlateDistanceAwareOverlayLifecycle.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.NamePlates;

/// <summary>
///     Retains stable NamePlate translation candidates and publishes the nearest
///     candidate that resolves to a visible live frame.
/// </summary>
internal sealed class NamePlateDistanceAwareOverlayLifecycle
{
  private readonly Dictionary<uint, NamePlateDistanceAwareOverlayCandidate> candidates = new();
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
  ///     Adds or replaces a retained translation candidate by entity identifier.
  /// </summary>
  /// <param name="candidate">The stable translation candidate.</param>
  internal void UpsertCandidate(NamePlateDistanceAwareOverlayCandidate candidate)
  {
    lock (this.gate)
    {
      this.candidates[candidate.EntityId] = candidate;
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

    NamePlateDistanceAwareOverlayCandidate? selectedCandidate = null;
    NamePlateDistanceAwareOverlayFrame? selectedFrame = null;
    foreach (var candidate in snapshot)
    {
      var frame = resolveLiveFrame(candidate);
      if (frame == null ||
          (selectedFrame != null &&
           (frame.Value.DistanceToCamera > selectedFrame.Value.DistanceToCamera ||
            (frame.Value.DistanceToCamera == selectedFrame.Value.DistanceToCamera &&
             candidate.EntityId >= selectedCandidate!.Value.EntityId))))
      {
        continue;
      }

      selectedCandidate = candidate;
      selectedFrame = frame;
    }

    if (selectedCandidate == null || selectedFrame == null)
    {
      ClearOverlay(overlay);
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
    return true;
  }

  /// <summary>
  ///     Clears all content and runtime presentation state from an overlay.
  /// </summary>
  /// <param name="overlay">The overlay to clear.</param>
  internal static void ClearOverlay(TranslationOverlay overlay)
  {
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
/// <param name="EntityId">The live object's stable entity identifier.</param>
/// <param name="OriginalText">The original NamePlate text.</param>
/// <param name="TranslatedText">The normalized translated NamePlate text.</param>
internal readonly record struct NamePlateDistanceAwareOverlayCandidate(
    uint EntityId,
    string OriginalText,
    string TranslatedText);
