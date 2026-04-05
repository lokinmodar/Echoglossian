// <copyright file="CutSceneSelectStringNodeResolvers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.CutSceneSelectString;

/// <summary>
///     Shared node walkers for the CutSceneSelectString addon.
/// </summary>
internal static unsafe class CutSceneSelectStringNodeResolvers
{
  /// <summary>
  ///     Resolves all readable text nodes in the addon tree, preserving tree order.
  /// </summary>
  /// <param name="addon">The live CutSceneSelectString addon instance.</param>
  /// <returns>The readable text-node addresses in tree order.</returns>
  public static List<nint> ResolveReadableTextNodes(AtkUnitBase* addon)
  {
    var results = new List<nint>();
    if (addon == null)
    {
      return results;
    }

    var seen = new HashSet<nint>();
    CollectReadableTextNodes(
        addon->UldManager.NodeList,
        (int)addon->UldManager.NodeListCount,
        results,
        seen);
    return results;
  }

  /// <summary>
  ///     Reads the visible text from a text node using the available payload
  ///     representations.
  /// </summary>
  /// <param name="textNode">The text node to inspect.</param>
  /// <returns>The readable text, or an empty string when unavailable.</returns>
  public static unsafe string ReadTextNode(AtkTextNode* textNode)
  {
    if (textNode == null)
    {
      return string.Empty;
    }

    var visibleText = textNode->NodeText.ToString();
    if (!string.IsNullOrWhiteSpace(visibleText))
    {
      return visibleText;
    }

    try
    {
      var originalText = textNode->OriginalTextPointer.AsReadOnlySeStringSpan().ExtractText();
      if (!string.IsNullOrWhiteSpace(originalText))
      {
        return originalText;
      }
    }
    catch
    {
      // Keep falling through to the legacy buffer read below.
    }

    try
    {
      return MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)textNode->NodeText.StringPtr.Value);
    }
    catch
    {
      return string.Empty;
    }
  }

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
