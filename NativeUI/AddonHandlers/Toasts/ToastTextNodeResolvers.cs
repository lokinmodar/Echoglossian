// <copyright file="ToastTextNodeResolvers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Toasts;

/// <summary>
///     Shared text-node resolvers for toast addons migrated to the new
///     AddonLifecycle handler model.
/// </summary>
internal static unsafe class ToastTextNodeResolvers
{
  private const int WideTextNodeId = 3;

  /// <summary>
  ///     Resolves the "_WideText" node used by the game for screen-info style
  ///     toast text.
  /// </summary>
  /// <param name="addon">The live "_WideText" addon instance.</param>
  /// <returns>The text node, or <see langword="null" />.</returns>
  public static AtkTextNode* ResolveWideTextNode(AtkUnitBase* addon)
  {
    return addon == null ? null : addon->GetTextNodeById(WideTextNodeId);
  }

  /// <summary>
  ///     Resolves the first text node found in the addon ULD node list.
  ///     This matches the historical Echoglossian approach for some toast-like
  ///     addons such as class/job change notifications.
  /// </summary>
  /// <param name="addon">The live addon instance.</param>
  /// <returns>The first text node, or <see langword="null" />.</returns>
  public static AtkTextNode* ResolveFirstTextNode(AtkUnitBase* addon)
  {
    if (addon == null)
    {
      return null;
    }

    for (var i = 0; i < addon->UldManager.NodeListCount; i++)
    {
      var node = addon->UldManager.NodeList[i];
      if (node != null && node->Type == NodeType.Text)
      {
        return (AtkTextNode*)node;
      }
    }

    return null;
  }
}
