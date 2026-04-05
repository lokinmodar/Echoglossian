// <copyright file="AddonTextNodeResolvers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Toasts;

/// <summary>
///     Shared text-node resolvers for single-text addons migrated to the new
///     AddonLifecycle handler model.
/// </summary>
internal static unsafe class AddonTextNodeResolvers
{
  private const int WideTextNodeId = 3;

  /// <summary>
  ///     Resolves the "_WideText" node used by the game for screen-info style
  ///     text.
  /// </summary>
  /// <param name="addon">The live "_WideText" addon instance.</param>
  /// <returns>The text node, or <see langword="null" />.</returns>
  public static AtkTextNode* ResolveWideTextNode(AtkUnitBase* addon)
  {
    return addon == null ? null : addon->GetTextNodeById(WideTextNodeId);
  }

  /// <summary>
  ///     Resolves the first text node found in the addon ULD node list.
  ///     This matches the historical Echoglossian approach for some single-text
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
  ///     Resolves the most suitable text node for the AreaText addon.
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
  ///     Resolves the best readable text node for the MiniTalk addon.
  ///     MiniTalk exposes a deeper component tree than the classic single-node
  ///     add-ons, so we walk the hierarchy until we find the first text node
  ///     that actually yields readable content.
  /// </summary>
  /// <param name="addon">The live "_MiniTalk" addon instance.</param>
  /// <returns>The best visible text node, or <see langword="null" />.</returns>
  public static AtkTextNode* ResolveMiniTalkTextNode(AtkUnitBase* addon)
  {
    return addon == null
        ? null
        : ResolveFirstReadableTextNode(addon->UldManager.NodeList, (int)addon->UldManager.NodeListCount);
  }

  /// <summary>
  ///     Resolves every readable MiniTalk bubble text node found in the live
  ///     addon tree.
  ///     The returned addresses are stable enough to act as per-bubble keys for
  ///     overlay state while the addon remains visible.
  /// </summary>
  /// <param name="addon">The live "_MiniTalk" addon instance.</param>
  /// <returns>
  ///     A list of readable, visible text-node addresses in tree order.
  /// </returns>
  public static List<nint> ResolveMiniTalkBubbleTextNodes(AtkUnitBase* addon)
  {
    var bubbleNodes = new List<nint>();
    if (addon == null)
    {
      return bubbleNodes;
    }

    var seen = new HashSet<nint>();
    CollectReadableTextNodes(
        addon->UldManager.NodeList,
        (int)addon->UldManager.NodeListCount,
        bubbleNodes,
        seen);
    return bubbleNodes;
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
  ///     Resolves the first text node in the addon hierarchy whose content can
  ///     actually be read as non-empty text.
  /// </summary>
  /// <param name="nodeList">The node list to inspect.</param>
  /// <param name="nodeCount">The number of nodes in the list.</param>
  /// <returns>The first readable text node, or <see langword="null" />.</returns>
  private static AtkTextNode* ResolveFirstReadableTextNode(
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
      var resolved = ResolveFirstReadableTextNode(node);
      if (resolved != null)
      {
        return resolved;
      }
    }

    return null;
  }

  /// <summary>
  ///     Collects readable, visible text nodes from a node list.
  /// </summary>
  /// <param name="nodeList">The node list to inspect.</param>
  /// <param name="nodeCount">The number of nodes in the list.</param>
  /// <param name="results">Receives the collected node addresses.</param>
  /// <param name="seen">Receives the node addresses already collected.</param>
  private static void CollectReadableTextNodes(
      AtkResNode** nodeList,
      int nodeCount,
      List<nint> results,
      HashSet<nint> seen)
  {
    if (nodeList == null || nodeCount <= 0)
    {
      return;
    }

    for (var i = 0; i < nodeCount; i++)
    {
      CollectReadableTextNodes(nodeList[i], results, seen);
    }
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

  /// <summary>
  ///     Resolves the first text node in a subtree whose content can actually
  ///     be read as non-empty text.
  /// </summary>
  /// <param name="node">The node to inspect.</param>
  /// <returns>The first readable text node, or <see langword="null" />.</returns>
  private static AtkTextNode* ResolveFirstReadableTextNode(AtkResNode* node)
  {
    if (node == null)
    {
      return null;
    }

    if (node->Type == NodeType.Text)
    {
      var textNode = (AtkTextNode*)node;
      if (HasReadableText(textNode))
      {
        return textNode;
      }
    }

    if ((ushort)node->Type >= 1000)
    {
      var componentNode = (AtkComponentNode*)node;
      if (componentNode->Component != null)
      {
        var nested = ResolveFirstReadableTextNode(
            componentNode->Component->UldManager.NodeList,
            (int)componentNode->Component->UldManager.NodeListCount);
        if (nested != null)
        {
          return nested;
        }
      }
    }

    var child = ResolveFirstReadableTextNode(node->ChildNode);
    if (child != null)
    {
      return child;
    }

    return ResolveFirstReadableTextNode(node->NextSiblingNode);
  }

  /// <summary>
  ///     Collects readable, visible text nodes from a single node subtree.
  /// </summary>
  /// <param name="node">The node to inspect.</param>
  /// <param name="results">Receives the collected node addresses.</param>
  /// <param name="seen">Receives the node addresses already collected.</param>
  private static void CollectReadableTextNodes(
      AtkResNode* node,
      List<nint> results,
      HashSet<nint> seen)
  {
    if (node == null)
    {
      return;
    }

    if (node->Type == NodeType.Text)
    {
      var textNode = (AtkTextNode*)node;
      var textNodeAddress = (nint)textNode;
      if (textNode->IsVisible() &&
          HasReadableText(textNode) &&
          seen.Add(textNodeAddress))
      {
        results.Add(textNodeAddress);
      }
    }

    if ((ushort)node->Type >= 1000)
    {
      var componentNode = (AtkComponentNode*)node;
      if (componentNode->Component != null)
      {
        CollectReadableTextNodes(
            componentNode->Component->UldManager.NodeList,
            (int)componentNode->Component->UldManager.NodeListCount,
            results,
            seen);
      }
    }

    CollectReadableTextNodes(node->ChildNode, results, seen);
    CollectReadableTextNodes(node->NextSiblingNode, results, seen);
  }

  /// <summary>
  ///     Determines whether a text node already exposes readable content.
  /// </summary>
  /// <param name="textNode">The text node to inspect.</param>
  /// <returns><see langword="true" /> when the node can be read as text.</returns>
  private static bool HasReadableText(AtkTextNode* textNode)
  {
    if (textNode == null)
    {
      return false;
    }

    if (!string.IsNullOrWhiteSpace(textNode->NodeText.ToString()))
    {
      return true;
    }

    try
    {
      var originalText = textNode->OriginalTextPointer.AsReadOnlySeStringSpan().ExtractText();
      if (!string.IsNullOrWhiteSpace(originalText))
      {
        return true;
      }
    }
    catch
    {
      // Keep falling through to the legacy buffer read below.
    }

    try
    {
      var legacyText = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)textNode->NodeText.StringPtr.Value);
      return !string.IsNullOrWhiteSpace(legacyText);
    }
    catch
    {
      return false;
    }
  }
}
