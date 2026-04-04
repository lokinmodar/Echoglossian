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
    return addon == null
        ? null
        : ResolveFirstTextNode(addon->UldManager.NodeList, (int)addon->UldManager.NodeListCount);
  }

  /// <summary>
  ///     Resolves the most suitable text node for the area toast addon.
  ///     The live addon can expose multiple text nodes, so we prefer the first
  ///     non-empty text node before falling back to the historical first-text
  ///     match.
  /// </summary>
  /// <param name="addon">The live "_AreaText" addon instance.</param>
  /// <returns>The best matching text node, or <see langword="null" />.</returns>
  public static AtkTextNode* ResolveAreaTextNode(AtkUnitBase* addon)
  {
    return addon == null
        ? null
        : ResolveVisibleTextNode(addon->UldManager.NodeList, (int)addon->UldManager.NodeListCount);
  }

  /// <summary>
  ///     Resolves the first text node found anywhere in the provided node list,
  ///     including nested component node lists.
  /// </summary>
  /// <param name="nodeList">The node list to inspect.</param>
  /// <param name="nodeCount">The number of nodes in the list.</param>
  /// <returns>The first text node, or <see langword="null" />.</returns>
  private static AtkTextNode* ResolveFirstTextNode(
      AtkResNode** nodeList,
      int nodeCount)
  {
    if (nodeList == null || nodeCount <= 0)
    {
      return null;
    }

    for (var i = 0; i < nodeCount; i++)
    {
      var node = nodeList[i];
      var resolved = ResolveFirstTextNode(node);
      if (resolved != null)
      {
        return resolved;
      }
    }

    return null;
  }

  /// <summary>
  ///     Resolves the first visible, non-empty text node found anywhere in the
  ///     provided node list, including nested component node lists.
  /// </summary>
  /// <param name="nodeList">The node list to inspect.</param>
  /// <param name="nodeCount">The number of nodes in the list.</param>
  /// <returns>The best visible text node, or <see langword="null" />.</returns>
  private static AtkTextNode* ResolveVisibleTextNode(
      AtkResNode** nodeList,
      int nodeCount)
  {
    if (nodeList == null || nodeCount <= 0)
    {
      return null;
    }

    AtkTextNode* firstTextNode = null;

    for (var i = 0; i < nodeCount; i++)
    {
      var node = nodeList[i];
      var resolved = ResolveVisibleTextNode(node, ref firstTextNode);
      if (resolved != null)
      {
        return resolved;
      }
    }

    return firstTextNode;
  }

  /// <summary>
  ///     Resolves the first text node found under a single node, recursing into
  ///     child, sibling, and component node lists.
  /// </summary>
  /// <param name="node">The node to inspect.</param>
  /// <returns>The first text node, or <see langword="null" />.</returns>
  private static AtkTextNode* ResolveFirstTextNode(AtkResNode* node)
  {
    if (node == null)
    {
      return null;
    }

    if (node->Type == NodeType.Text)
    {
      return (AtkTextNode*)node;
    }

    if ((ushort)node->Type >= 1000)
    {
      var componentNode = (AtkComponentNode*)node;
      if (componentNode->Component != null)
      {
        var nested = ResolveFirstTextNode(
            componentNode->Component->UldManager.NodeList,
            (int)componentNode->Component->UldManager.NodeListCount);
        if (nested != null)
        {
          return nested;
        }
      }
    }

    var child = ResolveFirstTextNode(node->ChildNode);
    if (child != null)
    {
      return child;
    }

    return ResolveFirstTextNode(node->NextSiblingNode);
  }

  /// <summary>
  ///     Resolves the best visible text node under a single node, recursing into
  ///     child, sibling, and component node lists.
  /// </summary>
  /// <param name="node">The node to inspect.</param>
  /// <param name="firstTextNode">
  ///     Receives the first text node encountered, even when empty or hidden.
  /// </param>
  /// <returns>The first visible, non-empty text node, or <see langword="null" />.</returns>
  private static AtkTextNode* ResolveVisibleTextNode(
      AtkResNode* node,
      ref AtkTextNode* firstTextNode)
  {
    if (node == null)
    {
      return null;
    }

    if (node->Type == NodeType.Text)
    {
      var textNode = (AtkTextNode*)node;
      if (firstTextNode == null)
      {
        firstTextNode = textNode;
      }

      if (textNode->IsVisible() && !textNode->NodeText.IsEmpty)
      {
        return textNode;
      }
    }

    if ((ushort)node->Type >= 1000)
    {
      var componentNode = (AtkComponentNode*)node;
      if (componentNode->Component != null)
      {
        var nested = ResolveVisibleTextNode(
            componentNode->Component->UldManager.NodeList,
            (int)componentNode->Component->UldManager.NodeListCount);
        if (nested != null)
        {
          return nested;
        }
      }
    }

    var child = ResolveVisibleTextNode(node->ChildNode, ref firstTextNode);
    if (child != null)
    {
      return child;
    }

    return ResolveVisibleTextNode(node->NextSiblingNode, ref firstTextNode);
  }
}
