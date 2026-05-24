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
    var textNodeWidth = textNode != null ? textNode->GetWidth() : (ushort)0;
    var textNodeHeight = textNode != null ? textNode->GetHeight() : (ushort)0;
    var textNodeTextFlags = textNode != null ? textNode->TextFlags : default;
    var textNodeFontSize = textNode != null ? textNode->FontSize : (byte)0;
    var textNodeOriginalX = textNode != null ? textNode->AtkResNode.GetXShort() : (short)0;
    var textNodeOriginalY = textNode != null ? textNode->AtkResNode.GetYShort() : (short)0;
    var snapshot = new NativeTextNodeLayoutSnapshot(
        textNode != null ? (nint)textNode : 0,
        textWidth,
        textHeight,
        textNodeWidth,
        textNodeHeight,
        textNodeTextFlags,
        textNodeFontSize,
        textNodeOriginalX,
        textNodeOriginalY)
    {
      AnchoredXOffset = anchoredXNode != null
          ? anchoredXNode->GetXShort() - textWidth
          : 0,
      AnchoredXNodeAddress = anchoredXNode != null ? (nint)anchoredXNode : 0,
      AnchoredXOriginal = anchoredXNode != null ? anchoredXNode->GetXShort() : (short)0,
    };

    if (secondaryContainerNode != null)
    {
      snapshot.SecondaryContainerAddress = (nint)secondaryContainerNode;
      snapshot.SecondaryContainerWidth = secondaryContainerNode->GetWidth();
      snapshot.SecondaryContainerHeight = secondaryContainerNode->GetHeight();
      snapshot.SecondaryContainerOriginalX = secondaryContainerNode->GetXShort();
      snapshot.SecondaryContainerOriginalY = secondaryContainerNode->GetYShort();
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
  /// <param name="secondaryContainerNode">
  ///     An optional secondary background node whose width may better represent
  ///     the stable visual bounds than the current text-node width.
  /// </param>
  /// <returns>The preferred wrap width to preserve.</returns>
  public static ushort ResolvePreferredWrapWidth(
      AtkTextNode* textNode,
      AtkResNode* primaryContainerNode = null,
      AtkResNode* secondaryContainerNode = null)
  {
    if (textNode == null)
    {
      return 0;
    }

    var currentWidth = textNode->GetWidth();
    var boundedContainerWidth = ResolveBoundedContainerWidth(
        primaryContainerNode,
        secondaryContainerNode);
    if (boundedContainerWidth > 0)
    {
      return boundedContainerWidth;
    }

    if (currentWidth > 0)
    {
      return currentWidth;
    }

    if (boundedContainerWidth > 0)
    {
      return boundedContainerWidth;
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

    if (preferredWrapWidth > 0 &&
        textNode->GetWidth() != preferredWrapWidth)
    {
      textNode->SetWidth(preferredWrapWidth);
    }

    TryMeasureTextNode(textNode, out var width, out var height);
    if (preferredWrapWidth > 0)
    {
      width = preferredWrapWidth;
    }

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
      bool allowWidthGrowth = false,
      bool restoreHorizontalCentering = true)
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

    if (restoreHorizontalCentering)
    {
      TryRestoreHorizontalCentering(snapshot);
    }
  }

  /// <summary>
  ///     Restores a previously captured native text-node layout snapshot after a
  ///     temporary translated replacement is no longer needed.
  /// </summary>
  /// <param name="snapshot">The layout snapshot captured before replacement.</param>
  /// <param name="originalText">
  ///     The original text that should be written back into the text node.
  /// </param>
  public static void RestoreLayoutSnapshot(
      NativeTextNodeLayoutSnapshot snapshot,
      string originalText,
      bool restoreText = true,
      bool restorePositions = true)
  {
    if (snapshot == null)
    {
      return;
    }

    var textNode = (AtkTextNode*)snapshot.TextNodeAddress;
    if (textNode != null)
    {
      textNode->TextFlags = snapshot.TextNodeTextFlags;
      if (snapshot.TextNodeFontSize > 0)
      {
        textNode->FontSize = snapshot.TextNodeFontSize;
      }

      if (restoreText &&
          !string.IsNullOrWhiteSpace(originalText))
      {
        textNode->SetText(originalText);
      }

      if (snapshot.TextNodeWidth > 0)
      {
        textNode->SetWidth(snapshot.TextNodeWidth);
      }

      if (snapshot.TextNodeHeight > 0)
      {
        ((AtkResNode*)textNode)->SetHeight(snapshot.TextNodeHeight);
      }

      if (restorePositions)
      {
        ((AtkResNode*)textNode)->SetXShort(snapshot.TextNodeOriginalX);
        ((AtkResNode*)textNode)->SetYShort(snapshot.TextNodeOriginalY);
      }
    }

    foreach (var ancestorSnapshot in snapshot.AncestorChain)
    {
      var ancestorNode = (AtkResNode*)ancestorSnapshot.NodeAddress;
      if (ancestorNode == null)
      {
        continue;
      }

      if (ancestorSnapshot.Width > 0)
      {
        ancestorNode->SetWidth(ancestorSnapshot.Width);
      }

      if (ancestorSnapshot.Height > 0)
      {
        ancestorNode->SetHeight(ancestorSnapshot.Height);
      }

      if (restorePositions)
      {
        ancestorNode->SetXShort(ancestorSnapshot.OriginalX);
        ancestorNode->SetYShort(ancestorSnapshot.OriginalY);
      }
    }

    if (snapshot.SecondaryContainerAddress != 0)
    {
      var secondaryContainerNode = (AtkResNode*)snapshot.SecondaryContainerAddress;
      if (secondaryContainerNode != null)
      {
        if (snapshot.SecondaryContainerWidth > 0)
        {
          secondaryContainerNode->SetWidth(snapshot.SecondaryContainerWidth);
        }

        if (snapshot.SecondaryContainerHeight > 0)
        {
          secondaryContainerNode->SetHeight(snapshot.SecondaryContainerHeight);
        }

        if (restorePositions)
        {
          secondaryContainerNode->SetXShort(snapshot.SecondaryContainerOriginalX);
          secondaryContainerNode->SetYShort(snapshot.SecondaryContainerOriginalY);
        }
      }
    }

    if (restorePositions &&
        snapshot.AnchoredXNodeAddress != 0)
    {
      var anchoredXNode = (AtkResNode*)snapshot.AnchoredXNodeAddress;
      if (anchoredXNode != null)
      {
        anchoredXNode->SetXShort(snapshot.AnchoredXOriginal);
      }
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
  public static NativeTextNodeLayoutSnapshot? ApplyTextReplacementWithInferredReflow(
      AtkUnitBase* addon,
      AtkTextNode* textNode,
      string replacementText,
      bool allowWidthGrowth = false,
      bool restoreHorizontalCentering = true,
      ushort additionalWrapWidth = 0)
  {
    if (textNode == null)
    {
      return null;
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
    var preferredWrapWidth = ResolvePreferredWrapWidth(
        textNode,
        containerNode,
        backgroundResNode);
    if (additionalWrapWidth > 0 && preferredWrapWidth > 0)
    {
      preferredWrapWidth = (ushort)Math.Min(
          ushort.MaxValue,
          preferredWrapWidth + additionalWrapWidth);
    }

    var resizeResult = ApplyWrappedTextAndMeasure(
        textNode,
        replacementText,
        preferredWrapWidth);
    ResizeFromSnapshot(
        snapshot,
        resizeResult,
        containerNode,
        backgroundResNode,
        allowWidthGrowth: allowWidthGrowth,
        restoreHorizontalCentering: restoreHorizontalCentering);
    return snapshot;
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
  ///     Resolves the widest stable visual width exposed by the surrounding
  ///     container nodes so repeated native replacements do not fall back to a
  ///     narrow internal wrapper when a larger background already represents the
  ///     intended visual balloon or panel width.
  /// </summary>
  /// <param name="primaryContainerNode">The primary owning container.</param>
  /// <param name="secondaryContainerNode">An optional secondary background node.</param>
  /// <returns>The widest non-zero container width available.</returns>
  private static ushort ResolveBoundedContainerWidth(
      AtkResNode* primaryContainerNode,
      AtkResNode* secondaryContainerNode)
  {
    ushort width = 0;

    if (primaryContainerNode != null)
    {
      width = primaryContainerNode->GetWidth();
    }

    if (secondaryContainerNode != null)
    {
      var secondaryWidth = secondaryContainerNode->GetWidth();
      if (secondaryWidth > 0 && secondaryWidth > width)
      {
        width = secondaryWidth;
      }
    }

    return width;
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
      if (snapshot.AncestorChain.Count == 0 && width > 0)
      {
        var leftPadding = Math.Max(0, (int)snapshot.TextNodeOriginalX);
        var rightPadding = Math.Max(
            0,
            width - leftPadding - snapshot.TextNodeWidth);
        snapshot.TextNodeWasHorizontallyCentered =
            Math.Abs(leftPadding - rightPadding) <= 4;
      }

      snapshot.AncestorChain.Add(
          new NativeTextNodeAncestorSnapshot(
              (nint)currentNode,
              width,
              height,
              currentNode->GetXShort(),
              currentNode->GetYShort(),
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

  /// <summary>
  ///     Re-centers the text node within its immediate wrapper when the original
  ///     layout was centered and width growth changed the wrapper size.
  /// </summary>
  /// <param name="snapshot">The captured layout snapshot.</param>
  private static void TryRestoreHorizontalCentering(
      NativeTextNodeLayoutSnapshot snapshot)
  {
    if (snapshot == null ||
        !snapshot.TextNodeWasHorizontallyCentered ||
        snapshot.TextNodeAddress == 0 ||
        snapshot.AncestorChain.Count == 0)
    {
      return;
    }

    var textNode = (AtkTextNode*)snapshot.TextNodeAddress;
    var immediateParent = (AtkResNode*)snapshot.AncestorChain[0].NodeAddress;
    if (textNode == null || immediateParent == null)
    {
      return;
    }

    var parentWidth = immediateParent->GetWidth();
    var textWidth = textNode->GetWidth();
    if (parentWidth == 0 || textWidth == 0 || parentWidth <= textWidth)
    {
      return;
    }

    var centeredX = (short)Math.Max(
        short.MinValue,
        Math.Min(
            short.MaxValue,
            (parentWidth - textWidth) / 2));
    ((AtkResNode*)textNode)->SetXShort(centeredX);
  }
}

/// <summary>
///     Captures the text and container sizing observed before native replacement.
/// </summary>
/// <param name="textWidth">The original text-node width.</param>
/// <param name="textHeight">The original text-node height.</param>
internal sealed class NativeTextNodeLayoutSnapshot
{
  /// <summary>
  ///     Initializes a new instance of the <see cref="NativeTextNodeLayoutSnapshot" /> class.
  /// </summary>
  /// <param name="textNodeAddress">The address of the mutated text node.</param>
  /// <param name="textWidth">The original measured text width.</param>
  /// <param name="textHeight">The original measured text height.</param>
  /// <param name="textNodeWidth">The original text-node width.</param>
  /// <param name="textNodeHeight">The original text-node height.</param>
  /// <param name="textNodeTextFlags">The original text-node flags.</param>
  /// <param name="textNodeFontSize">The original text-node font size.</param>
  /// <param name="textNodeOriginalX">The original text-node X position.</param>
  /// <param name="textNodeOriginalY">The original text-node Y position.</param>
  public NativeTextNodeLayoutSnapshot(
      nint textNodeAddress,
      ushort textWidth,
      ushort textHeight,
      ushort textNodeWidth,
      ushort textNodeHeight,
      TextFlags textNodeTextFlags,
      byte textNodeFontSize,
      short textNodeOriginalX,
      short textNodeOriginalY)
  {
    this.TextNodeAddress = textNodeAddress;
    this.TextWidth = textWidth;
    this.TextHeight = textHeight;
    this.TextNodeWidth = textNodeWidth;
    this.TextNodeHeight = textNodeHeight;
    this.TextNodeTextFlags = textNodeTextFlags;
    this.TextNodeFontSize = textNodeFontSize;
    this.TextNodeOriginalX = textNodeOriginalX;
    this.TextNodeOriginalY = textNodeOriginalY;
  }

  /// <summary>
  ///     Gets the native address of the mutated text node.
  /// </summary>
  public nint TextNodeAddress { get; }

  /// <summary>
  ///     Gets the original text-node width.
  /// </summary>
  public ushort TextWidth { get; }

  /// <summary>
  ///     Gets the original text-node height.
  /// </summary>
  public ushort TextHeight { get; }

  /// <summary>
  ///     Gets the original width assigned to the text node itself.
  /// </summary>
  public ushort TextNodeWidth { get; }

  /// <summary>
  ///     Gets the original height assigned to the text node itself.
  /// </summary>
  public ushort TextNodeHeight { get; }

  /// <summary>
  ///     Gets the original text-node flags.
  /// </summary>
  public TextFlags TextNodeTextFlags { get; }

  /// <summary>
  ///     Gets the original text-node font size.
  /// </summary>
  public byte TextNodeFontSize { get; }

  /// <summary>
  ///     Gets the original X position assigned to the text node itself.
  /// </summary>
  public short TextNodeOriginalX { get; }

  /// <summary>
  ///     Gets the original Y position assigned to the text node itself.
  /// </summary>
  public short TextNodeOriginalY { get; }

  /// <summary>
  ///     Gets or sets a value indicating whether the text node was originally
  ///     centered inside its immediate parent.
  /// </summary>
  public bool TextNodeWasHorizontallyCentered { get; set; }

  /// <summary>
  ///     Gets the wrapper chain that should be resized after a native text
  ///     replacement.
  /// </summary>
  public List<NativeTextNodeAncestorSnapshot> AncestorChain { get; } = [];

  /// <summary>
  ///     Gets or sets the native address of the secondary container node.
  /// </summary>
  public nint SecondaryContainerAddress { get; set; }

  /// <summary>
  ///     Gets or sets the secondary container width.
  /// </summary>
  public ushort SecondaryContainerWidth { get; set; }

  /// <summary>
  ///     Gets or sets the secondary container height.
  /// </summary>
  public ushort SecondaryContainerHeight { get; set; }

  /// <summary>
  ///     Gets or sets the original X position of the secondary container.
  /// </summary>
  public short SecondaryContainerOriginalX { get; set; }

  /// <summary>
  ///     Gets or sets the original Y position of the secondary container.
  /// </summary>
  public short SecondaryContainerOriginalY { get; set; }

  /// <summary>
  ///     Gets or sets the native address of the anchored X node.
  /// </summary>
  public nint AnchoredXNodeAddress { get; set; }

  /// <summary>
  ///     Gets or sets the X offset between an anchored sibling node and the text
  ///     width.
  /// </summary>
  public int AnchoredXOffset { get; set; }

  /// <summary>
  ///     Gets or sets the original X position of the anchored sibling node.
  /// </summary>
  public short AnchoredXOriginal { get; set; }

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
/// <param name="OriginalX">The original X position of the wrapper node.</param>
/// <param name="OriginalY">The original Y position of the wrapper node.</param>
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
    short OriginalX,
    short OriginalY,
    int HorizontalPadding,
    int VerticalPadding);
