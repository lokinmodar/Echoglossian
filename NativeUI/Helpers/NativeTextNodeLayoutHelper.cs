// <copyright file="NativeTextNodeLayoutHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Shared helpers for native text-node replacement paths that need the game
///     UI to wrap multi-line text and keep nearby background nodes in sync.
/// </summary>
internal static unsafe class NativeTextNodeLayoutHelper
{
  /// <summary>
  ///     Captures the current text-node and container sizing so a later native
  ///     replacement can grow the layout using inferred padding instead of fixed
  ///     magic numbers.
  /// </summary>
  /// <param name="textNode">The text node whose layout should be captured.</param>
  /// <param name="primaryContainerNode">
  ///     The main container node that visually owns the text node.
  /// </param>
  /// <param name="secondaryContainerNode">
  ///     An optional secondary background node, such as a nine-grid.
  /// </param>
  /// <param name="anchoredXNode">
  ///     An optional sibling node whose X position is anchored to the text width.
  /// </param>
  /// <returns>The captured layout snapshot.</returns>
  public static NativeTextNodeLayoutSnapshot CaptureLayoutSnapshot(
      AtkTextNode* textNode,
      AtkResNode* primaryContainerNode = null,
      AtkResNode* secondaryContainerNode = null,
      AtkResNode* anchoredXNode = null)
  {
    TryMeasureTextNode(textNode, out var textWidth, out var textHeight);
    var snapshot = new NativeTextNodeLayoutSnapshot(textWidth, textHeight)
    {
      AnchoredXOffset = anchoredXNode != null
          ? anchoredXNode->GetXShort() - textWidth
          : 0,
    };

    if (secondaryContainerNode != null)
    {
      snapshot.SecondaryContainerWidth = secondaryContainerNode->GetWidth();
      snapshot.SecondaryContainerHeight = secondaryContainerNode->GetHeight();
    }

    CaptureAncestorChain(
        snapshot,
        textNode,
        primaryContainerNode);
    return snapshot;
  }

  /// <summary>
  ///     Resolves the current wrap width that should be reused for a native text
  ///     replacement.
  /// </summary>
  /// <param name="textNode">The text node being replaced.</param>
  /// <param name="primaryContainerNode">
  ///     The main container node that owns the text node.
  /// </param>
  /// <returns>The preferred wrap width to preserve.</returns>
  public static ushort ResolvePreferredWrapWidth(
      AtkTextNode* textNode,
      AtkResNode* primaryContainerNode = null)
  {
    if (textNode == null)
    {
      return 0;
    }

    var currentWidth = textNode->GetWidth();
    if (currentWidth > 0)
    {
      return currentWidth;
    }

    if (primaryContainerNode != null)
    {
      return primaryContainerNode->GetWidth();
    }

    var parentNode = textNode->AtkResNode.ParentNode;
    return parentNode != null ? parentNode->GetWidth() : (ushort)0;
  }

  /// <summary>
  ///     Applies translated text to a node using the existing wrap width and the
  ///     minimum multiline flags required for the game to recompute size.
  /// </summary>
  /// <param name="textNode">The text node that should receive the translation.</param>
  /// <param name="replacementText">The translated text to write back.</param>
  /// <param name="preferredWrapWidth">
  ///     The width that should be preserved for wrapping.
  /// </param>
  /// <returns>The measured size after the text replacement.</returns>
  public static NativeTextNodeResizeResult ApplyWrappedTextAndMeasure(
      AtkTextNode* textNode,
      string replacementText,
      ushort preferredWrapWidth)
  {
    if (textNode == null)
    {
      return default;
    }

    textNode->TextFlags |= TextFlags.WordWrap
                           | TextFlags.MultiLine
                           | TextFlags.AutoAdjustNodeSize;

    if (preferredWrapWidth > 0)
    {
      textNode->SetWidth(preferredWrapWidth);
    }

    textNode->SetText(replacementText);
    textNode->ResizeNodeForCurrentText();

    TryMeasureTextNode(textNode, out var width, out var height);
    return new NativeTextNodeResizeResult(width, height);
  }

  /// <summary>
  ///     Resizes the supplied container nodes using the text delta captured in a
  ///     pre-replacement layout snapshot.
  /// </summary>
  /// <param name="snapshot">The layout captured before native replacement.</param>
  /// <param name="resizeResult">The measured text size after replacement.</param>
  /// <param name="primaryContainerNode">
  ///     The main visual container that should grow with the text.
  /// </param>
  /// <param name="secondaryContainerNode">
  ///     An optional secondary background node, such as a nine-grid.
  /// </param>
  /// <param name="anchoredXNode">
  ///     An optional sibling node whose X position should continue to track the
  ///     text width.
  /// </param>
  /// <param name="allowWidthGrowth">
  ///     Whether the helper may grow container widths when the wrapped text width
  ///     exceeds the original layout.
  /// </param>
  public static void ResizeFromSnapshot(
      NativeTextNodeLayoutSnapshot snapshot,
      NativeTextNodeResizeResult resizeResult,
      AtkResNode* primaryContainerNode = null,
      AtkResNode* secondaryContainerNode = null,
      AtkResNode* anchoredXNode = null,
      bool allowWidthGrowth = false)
  {
    var childWidth = resizeResult.Width;
    var childHeight = resizeResult.Height;

    foreach (var ancestorSnapshot in snapshot.AncestorChain)
    {
      var ancestorNode = (AtkResNode*)ancestorSnapshot.NodeAddress;
      if (ancestorNode == null)
      {
        continue;
      }

      if (allowWidthGrowth && ancestorSnapshot.Width > 0)
      {
        var ancestorWidth = Math.Max(
            ancestorSnapshot.Width,
            childWidth + ancestorSnapshot.HorizontalPadding);
        ancestorNode->SetWidth((ushort)Math.Min(ushort.MaxValue, ancestorWidth));
        childWidth = (ushort)Math.Min(ushort.MaxValue, ancestorWidth);
      }
      else if (ancestorSnapshot.Width > 0)
      {
        childWidth = ancestorSnapshot.Width;
      }

      if (ancestorSnapshot.Height > 0)
      {
        var ancestorHeight = Math.Max(
            1,
            childHeight + ancestorSnapshot.VerticalPadding);
        ancestorNode->SetHeight((ushort)Math.Min(ushort.MaxValue, ancestorHeight));
        childHeight = (ushort)Math.Min(ushort.MaxValue, ancestorHeight);
      }
    }

    if (secondaryContainerNode != null)
    {
      if (allowWidthGrowth && snapshot.SecondaryContainerWidth > 0)
      {
        var secondaryWidth = Math.Max(
            snapshot.SecondaryContainerWidth,
            resizeResult.Width + snapshot.SecondaryHorizontalPadding);
        secondaryContainerNode->SetWidth((ushort)Math.Min(ushort.MaxValue, secondaryWidth));
      }

      if (snapshot.SecondaryContainerHeight > 0)
      {
        var secondaryHeight = Math.Max(
            1,
            resizeResult.Height + snapshot.SecondaryVerticalPadding);
        secondaryContainerNode->SetHeight((ushort)Math.Min(ushort.MaxValue, secondaryHeight));
      }
    }

    if (anchoredXNode != null)
    {
      anchoredXNode->SetXShort(
          (short)Math.Max(short.MinValue, Math.Min(
              short.MaxValue,
              childWidth + snapshot.AnchoredXOffset)));
    }
  }

  /// <summary>
  ///     Resolves the nearest component-backed container for a text node and the
  ///     first nine-grid background nested inside that container.
  /// </summary>
  /// <param name="addon">The live addon that owns the text node.</param>
  /// <param name="textNode">The text node being translated.</param>
  /// <param name="containerNode">
  ///     Receives the primary container node that should grow with the text.
  /// </param>
  /// <param name="backgroundNode">
  ///     Receives the first nested nine-grid background node, when present.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> when at least one layout node was resolved;
  ///     otherwise, <see langword="false" />.
  /// </returns>
  public static bool TryResolveContainerNodes(
      AtkUnitBase* addon,
      AtkTextNode* textNode,
      out AtkResNode* containerNode,
      out AtkNineGridNode* backgroundNode)
  {
    containerNode = null;
    backgroundNode = null;

    if (textNode == null)
    {
      return false;
    }

    var componentNode = FindNearestAncestorComponentNode((AtkResNode*)textNode);
    if (componentNode != null)
    {
      containerNode = &componentNode->AtkResNode;
      backgroundNode = FindFirstNineGridNode(componentNode);
      return containerNode != null || backgroundNode != null;
    }

    if (addon == null)
    {
      return false;
    }

    containerNode = addon->RootNode;
    backgroundNode = FindFirstNineGridNode(
        addon->UldManager.NodeList,
        (int)addon->UldManager.NodeListCount);
    return containerNode != null || backgroundNode != null;
  }

  /// <summary>
  ///     Applies translated text to a node and grows its wrapper chain plus the
  ///     nearest nine-grid background using inferred padding from the current
  ///     addon layout.
  /// </summary>
  /// <param name="addon">The visible addon that owns the text node.</param>
  /// <param name="textNode">The text node receiving the translation.</param>
  /// <param name="replacementText">The translated text to write back.</param>
  /// <param name="allowWidthGrowth">
  ///     Whether wrapper widths may grow when the current wrap width is
  ///     insufficient.
  /// </param>
  public static void ApplyTextReplacementWithInferredReflow(
      AtkUnitBase* addon,
      AtkTextNode* textNode,
      string replacementText,
      bool allowWidthGrowth = false)
  {
    if (textNode == null)
    {
      return;
    }

    TryResolveContainerNodes(
        addon,
        textNode,
        out var containerNode,
        out var backgroundNode);

    var backgroundResNode = backgroundNode != null
        ? &backgroundNode->AtkResNode
        : null;
    var snapshot = CaptureLayoutSnapshot(
        textNode,
        containerNode,
        backgroundResNode);
    var preferredWrapWidth = ResolvePreferredWrapWidth(textNode, containerNode);
    var resizeResult = ApplyWrappedTextAndMeasure(
        textNode,
        replacementText,
        preferredWrapWidth);
    ResizeFromSnapshot(
        snapshot,
        resizeResult,
        containerNode,
        backgroundResNode,
        allowWidthGrowth: allowWidthGrowth);
  }

  /// <summary>
  ///     Measures the current node size using the live node dimensions first and
  ///     falling back to text draw size when the node has not been sized yet.
  /// </summary>
  /// <param name="textNode">The text node to measure.</param>
  /// <param name="width">Receives the measured width.</param>
  /// <param name="height">Receives the measured height.</param>
  public static void TryMeasureTextNode(
      AtkTextNode* textNode,
      out ushort width,
      out ushort height)
  {
    width = 0;
    height = 0;

    if (textNode == null)
    {
      return;
    }

    width = textNode->GetWidth();
    height = textNode->GetHeight();

    if (width > 0 && height > 0)
    {
      return;
    }

    ushort measuredWidth = 0;
    ushort measuredHeight = 0;
    textNode->GetTextDrawSize(&measuredWidth, &measuredHeight);
    width = measuredWidth;
    height = measuredHeight;
  }

  /// <summary>
  ///     Walks up the parent chain until a component-backed ancestor is found.
  /// </summary>
  /// <param name="node">The node whose ancestors should be inspected.</param>
  /// <returns>
  ///     The nearest component ancestor, or <see langword="null" />.
  /// </returns>
  private static AtkComponentNode* FindNearestAncestorComponentNode(AtkResNode* node)
  {
    var currentNode = node != null ? node->ParentNode : null;
    while (currentNode != null)
    {
      if ((ushort)currentNode->Type >= 1000)
      {
        var componentNode = (AtkComponentNode*)currentNode;
        if (componentNode->Component != null)
        {
          return componentNode;
        }
      }

      currentNode = currentNode->ParentNode;
    }

    return null;
  }

  /// <summary>
  ///     Resolves the first nine-grid node nested inside a component node.
  /// </summary>
  /// <param name="componentNode">The component node to inspect.</param>
  /// <returns>
  ///     The first nested nine-grid node, or <see langword="null" />.
  /// </returns>
  private static AtkNineGridNode* FindFirstNineGridNode(AtkComponentNode* componentNode)
  {
    if (componentNode == null || componentNode->Component == null)
    {
      return null;
    }

    return FindFirstNineGridNode(
        componentNode->Component->UldManager.NodeList,
        (int)componentNode->Component->UldManager.NodeListCount);
  }

  /// <summary>
  ///     Resolves the first nine-grid node reachable from a node list.
  /// </summary>
  /// <param name="nodeList">The node list to inspect.</param>
  /// <param name="nodeCount">The number of nodes in the list.</param>
  /// <returns>
  ///     The first nested nine-grid node, or <see langword="null" />.
  /// </returns>
  private static AtkNineGridNode* FindFirstNineGridNode(
      AtkResNode** nodeList,
      int nodeCount)
  {
    if (nodeList == null || nodeCount <= 0)
    {
      return null;
    }

    for (var i = 0; i < nodeCount; i++)
    {
      var foundNode = FindFirstNineGridNode(nodeList[i]);
      if (foundNode != null)
      {
        return foundNode;
      }
    }

    return null;
  }

  /// <summary>
  ///     Resolves the first nine-grid node reachable from a node.
  /// </summary>
  /// <param name="node">The node to inspect.</param>
  /// <returns>
  ///     The first nested nine-grid node, or <see langword="null" />.
  /// </returns>
  private static AtkNineGridNode* FindFirstNineGridNode(AtkResNode* node)
  {
    if (node == null)
    {
      return null;
    }

    if (node->Type == NodeType.NineGrid)
    {
      return (AtkNineGridNode*)node;
    }

    if ((ushort)node->Type >= 1000)
    {
      var componentNode = (AtkComponentNode*)node;
      var nestedNode = FindFirstNineGridNode(componentNode);
      if (nestedNode != null)
      {
        return nestedNode;
      }
    }

    var childNode = FindFirstNineGridNode(node->ChildNode);
    if (childNode != null)
    {
      return childNode;
    }

    return FindFirstNineGridNode(node->NextSiblingNode);
  }

  /// <summary>
  ///     Captures the wrapper chain between a text node and the container that
  ///     visually owns it so each wrapper can be grown by the same delta later.
  /// </summary>
  /// <param name="snapshot">The snapshot receiving wrapper metadata.</param>
  /// <param name="textNode">The text node whose wrappers should be captured.</param>
  /// <param name="stopNode">
  ///     The top-most wrapper to include in the captured chain.
  /// </param>
  private static void CaptureAncestorChain(
      NativeTextNodeLayoutSnapshot snapshot,
      AtkTextNode* textNode,
      AtkResNode* stopNode)
  {
    if (snapshot == null || textNode == null)
    {
      return;
    }

    var childWidth = snapshot.TextWidth;
    var childHeight = snapshot.TextHeight;
    var currentNode = textNode->AtkResNode.ParentNode;

    while (currentNode != null)
    {
      var width = currentNode->GetWidth();
      var height = currentNode->GetHeight();
      snapshot.AncestorChain.Add(
          new NativeTextNodeAncestorSnapshot(
              (nint)currentNode,
              width,
              height,
              Math.Max(0, width - childWidth),
              Math.Max(0, height - childHeight)));

      childWidth = width;
      childHeight = height;

      if (currentNode == stopNode)
      {
        break;
      }

      currentNode = currentNode->ParentNode;
    }
  }
}

/// <summary>
///     Captures the text and container sizing observed before native replacement.
/// </summary>
/// <param name="textWidth">The original text-node width.</param>
/// <param name="textHeight">The original text-node height.</param>
internal sealed class NativeTextNodeLayoutSnapshot(
    ushort textWidth,
    ushort textHeight)
{
  /// <summary>
  ///     Gets the original text-node width.
  /// </summary>
  public ushort TextWidth { get; } = textWidth;

  /// <summary>
  ///     Gets the original text-node height.
  /// </summary>
  public ushort TextHeight { get; } = textHeight;

  /// <summary>
  ///     Gets the wrapper chain that should be resized after a native text
  ///     replacement.
  /// </summary>
  public List<NativeTextNodeAncestorSnapshot> AncestorChain { get; } = [];

  /// <summary>
  ///     Gets or sets the secondary container width.
  /// </summary>
  public ushort SecondaryContainerWidth { get; set; }

  /// <summary>
  ///     Gets or sets the secondary container height.
  /// </summary>
  public ushort SecondaryContainerHeight { get; set; }

  /// <summary>
  ///     Gets or sets the X offset between an anchored sibling node and the text
  ///     width.
  /// </summary>
  public int AnchoredXOffset { get; set; }

  /// <summary>
  ///     Gets the horizontal padding between the text node and the secondary
  ///     container.
  /// </summary>
  public int SecondaryHorizontalPadding => Math.Max(0, this.SecondaryContainerWidth - this.TextWidth);

  /// <summary>
  ///     Gets the vertical padding between the text node and the secondary
  ///     container.
  /// </summary>
  public int SecondaryVerticalPadding => Math.Max(0, this.SecondaryContainerHeight - this.TextHeight);
}

/// <summary>
///     Represents the measured text-node size after a native replacement.
/// </summary>
/// <param name="Width">The measured text-node width.</param>
/// <param name="Height">The measured text-node height.</param>
internal readonly record struct NativeTextNodeResizeResult(
    ushort Width,
    ushort Height);

/// <summary>
///     Captures one ancestor wrapper in a text-node layout chain.
/// </summary>
/// <param name="NodeAddress">The native address of the wrapper node.</param>
/// <param name="Width">The original wrapper width.</param>
/// <param name="Height">The original wrapper height.</param>
/// <param name="HorizontalPadding">
///     The original horizontal padding between this wrapper and its child.
/// </param>
/// <param name="VerticalPadding">
///     The original vertical padding between this wrapper and its child.
/// </param>
internal readonly record struct NativeTextNodeAncestorSnapshot(
    nint NodeAddress,
    ushort Width,
    ushort Height,
    int HorizontalPadding,
    int VerticalPadding);
