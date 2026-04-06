// <copyright file="TranslationOverlaySyncHelpers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

public partial class Echoglossian
{
  /// <summary>
  /// Updates the shared toast overlay bounds using a live "_WideText" addon
  /// instance received from AddonLifecycle.
  /// </summary>
  /// <param name="addon">The live "_WideText" addon.</param>
  private unsafe void SyncWideTextToastOverlayBounds(
      AtkUnitBase* addon,
      AtkTextNode* textNode)
  {
    this.UpdateToastOverlayBounds(this.toastOverlay, addon, textNode);
  }

  /// <summary>
  /// Updates the error toast overlay bounds using a live "_TextError" addon
  /// instance received from AddonLifecycle.
  /// </summary>
  /// <param name="addon">The live "_TextError" addon.</param>
  private unsafe void SyncErrorToastOverlayBounds(
      AtkUnitBase* addon,
      AtkTextNode* textNode)
  {
    this.UpdateToastOverlayBounds(this.errorToastOverlay, addon, textNode);
  }

  /// <summary>
  /// Updates the area toast overlay bounds using a live "_AreaText" addon
  /// instance received from AddonLifecycle.
  /// </summary>
  /// <param name="addon">The live "_AreaText" addon.</param>
  private unsafe void SyncAreaToastOverlayBounds(
      AtkUnitBase* addon,
      AtkTextNode* textNode)
  {
    this.UpdateToastOverlayBounds(this.areaToastOverlay, addon, textNode);
  }

  /// <summary>
  /// Updates the class/job change toast overlay bounds using a live
  /// "_TextClassChange" addon instance received from AddonLifecycle.
  /// </summary>
  /// <param name="addon">The live "_TextClassChange" addon.</param>
  private unsafe void SyncClassChangeToastOverlayBounds(
      AtkUnitBase* addon,
      AtkTextNode* textNode)
  {
    this.UpdateToastOverlayBounds(this.classChangeToastOverlay, addon, textNode);
  }

  /// <summary>
  /// Updates the text gimmick hint overlay bounds using a live addon instance
  /// received from AddonLifecycle.
  /// </summary>
  /// <param name="addon">The live "_TextGimmickHint" addon.</param>
  private unsafe void SyncTextGimmickHintToastOverlayBounds(
      AtkUnitBase* addon,
      AtkTextNode* textNode)
  {
    this.UpdateToastOverlayBounds(this.textGimmickHintOverlay, addon, textNode);
  }
}
