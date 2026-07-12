// <copyright file="VisibleStorySurfaceText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Resolves localized user-facing strings for visible story-surface
///     debugger and explicit retranslation flows.
/// </summary>
public static class VisibleStorySurfaceText
{
  /// <summary>
  ///     Resolves the localized display name for one visible story surface.
  /// </summary>
  /// <param name="surface">The visible story surface.</param>
  /// <returns>The localized surface display name.</returns>
  public static string ResolveSurfaceName(VisibleStorySurfaceKind surface)
  {
    return surface switch
    {
      VisibleStorySurfaceKind.Talk =>
          Resources.TranslatorDebuggerVisibleStorySurfaceNameTalk,
      VisibleStorySurfaceKind.BattleTalk =>
          Resources.TranslatorDebuggerVisibleStorySurfaceNameBattleTalk,
      VisibleStorySurfaceKind.TalkSubtitle =>
          Resources.TranslatorDebuggerVisibleStorySurfaceNameTalkSubtitle,
      VisibleStorySurfaceKind.CutSceneSelectString =>
          Resources.TranslatorDebuggerVisibleStorySurfaceNameCutSceneSelectString,
      VisibleStorySurfaceKind.TextGimmickHint =>
          Resources.TranslatorDebuggerVisibleStorySurfaceNameTextGimmickHint,
      _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null),
    };
  }

  /// <summary>
  ///     Resolves the localized provenance label for one diagnostics snapshot.
  /// </summary>
  /// <param name="provenance">The provenance kind to format.</param>
  /// <returns>The localized provenance label.</returns>
  public static string ResolveProvenanceLabel(
      VisibleStorySurfaceProvenanceKind provenance)
  {
    return provenance switch
    {
      VisibleStorySurfaceProvenanceKind.DbReuse =>
          Resources.TranslatorDebuggerVisibleStorySurfaceProvenanceDbReuse,
      VisibleStorySurfaceProvenanceKind.FreshLiveTranslation =>
          Resources.TranslatorDebuggerVisibleStorySurfaceProvenanceFreshLiveTranslation,
      VisibleStorySurfaceProvenanceKind.FreshLiveTranslationRuntimeOnlyDialogueContext =>
          Resources.TranslatorDebuggerVisibleStorySurfaceProvenanceFreshLiveTranslationRuntimeOnlyContext,
      _ => throw new ArgumentOutOfRangeException(
          nameof(provenance),
          provenance,
          null),
    };
  }

  /// <summary>
  ///     Builds the user-facing message shown when no visible text is available
  ///     for one story surface.
  /// </summary>
  /// <param name="surface">The visible story surface.</param>
  /// <returns>The localized unavailable message.</returns>
  public static string GetNoVisibleTextMessage(VisibleStorySurfaceKind surface)
  {
    return string.Format(
        CultureInfo.CurrentCulture,
        Resources.TranslatorDebuggerVisibleStorySurfaceNoVisibleText,
        ResolveSurfaceName(surface));
  }

  /// <summary>
  ///     Builds the user-facing message shown when a visible retranslation did
  ///     not produce usable translated content.
  /// </summary>
  /// <param name="surface">The visible story surface.</param>
  /// <returns>The localized unusable-result message.</returns>
  public static string GetNoUsableTranslationMessage(
      VisibleStorySurfaceKind surface)
  {
    return string.Format(
        CultureInfo.CurrentCulture,
        Resources.TranslatorDebuggerVisibleStorySurfaceNoUsableTranslation,
        ResolveSurfaceName(surface));
  }

  /// <summary>
  ///     Builds the user-facing message shown when a live refresh applied but
  ///     persistence failed.
  /// </summary>
  /// <param name="surface">The visible story surface.</param>
  /// <param name="persistenceResult">The persistence-layer detail.</param>
  /// <returns>The localized persistence failure message.</returns>
  public static string GetPersistenceFailedMessage(
      VisibleStorySurfaceKind surface,
      string persistenceResult)
  {
    return string.Format(
        CultureInfo.CurrentCulture,
        Resources.TranslatorDebuggerVisibleStorySurfacePersistenceFailed,
        ResolveSurfaceName(surface),
        persistenceResult);
  }

  /// <summary>
  ///     Builds the user-facing message shown when a refreshed result was
  ///     persisted but the visible source changed before apply.
  /// </summary>
  /// <param name="surface">The visible story surface.</param>
  /// <returns>The localized stale-apply message.</returns>
  public static string GetPersistedButVisibleChangedMessage(
      VisibleStorySurfaceKind surface)
  {
    return string.Format(
        CultureInfo.CurrentCulture,
        Resources.TranslatorDebuggerVisibleStorySurfacePersistedButVisibleChanged,
        ResolveSurfaceName(surface));
  }

  /// <summary>
  ///     Builds the user-facing message shown when explicit retranslation
  ///     succeeds and is persisted.
  /// </summary>
  /// <param name="surface">The visible story surface.</param>
  /// <returns>The localized success message.</returns>
  public static string GetRetranslatedAndPersistedMessage(
      VisibleStorySurfaceKind surface)
  {
    return string.Format(
        CultureInfo.CurrentCulture,
        Resources.TranslatorDebuggerVisibleStorySurfaceRetranslatedAndPersisted,
        ResolveSurfaceName(surface));
  }

  /// <summary>
  ///     Builds the user-facing message shown when explicit retranslation
  ///     fails before a usable result can be applied.
  /// </summary>
  /// <param name="surface">The visible story surface.</param>
  /// <returns>The localized failure message.</returns>
  public static string GetRetranslationFailedMessage(
      VisibleStorySurfaceKind surface)
  {
    return string.Format(
        CultureInfo.CurrentCulture,
        Resources.TranslatorDebuggerVisibleStorySurfaceRetranslationFailed,
        ResolveSurfaceName(surface));
  }

  /// <summary>
  ///     Gets the user-facing message shown when no visible story surface is
  ///     currently available for explicit retranslation.
  /// </summary>
  /// <returns>The localized unavailable message.</returns>
  public static string GetNoVisibleSurfaceAvailableMessage()
  {
    return Resources.TranslatorDebuggerVisibleStorySurfaceNoVisibleSurfaceAvailable;
  }

  /// <summary>
  ///     Gets the user-facing message shown when a visible retranslation task
  ///     faults before it returns a structured result.
  /// </summary>
  /// <returns>The localized unexpected-failure message.</returns>
  public static string GetUnexpectedFailureMessage()
  {
    return Resources.TranslatorDebuggerVisibleStorySurfaceUnexpectedFailure;
  }

  /// <summary>
  ///     Gets the user-facing message shown when a visible retranslation task
  ///     is canceled.
  /// </summary>
  /// <returns>The localized cancellation message.</returns>
  public static string GetCanceledMessage()
  {
    return Resources.TranslatorDebuggerVisibleStorySurfaceCanceled;
  }
}
