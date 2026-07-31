// <copyright file="NativeTextNodeLayoutHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text;

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
      AnchoredXWidth = anchoredXNode != null ? anchoredXNode->GetWidth() : (ushort)0,
      AnchoredXRightPaddingFromSecondary = secondaryContainerNode != null && anchoredXNode != null
          ? ((secondaryContainerNode->GetXShort() + secondaryContainerNode->GetWidth())
             - (anchoredXNode->GetXShort() + anchoredXNode->GetWidth()))
          : 0,
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
    CaptureDetachedPrimaryContainer(
        snapshot,
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
  /// <param name="allowWidthGrowth">
  ///     Whether the text node itself may widen to the measured candidate text
  ///     width instead of staying clamped to the preserved historical width.
  /// </param>
  /// <param name="measureReplacementWidthBeforeApply">
  ///     Whether the candidate replacement text should be measured before it is
  ///     applied so width growth can use that draw width instead of relying
  ///     only on the historical wrap width.
  /// </param>
  /// <returns>The measured size after the text replacement.</returns>
  public static NativeTextNodeResizeResult ApplyWrappedTextAndMeasure(
      AtkTextNode* textNode,
      string replacementText,
      ushort preferredWrapWidth,
      bool allowWidthGrowth = false,
      bool measureReplacementWidthBeforeApply = false)
  {
    if (textNode == null)
    {
      return default;
    }

    var resolvedWrapWidth = ResolveReplacementWrapWidth(
        preferredWrapWidth,
        measureReplacementWidthBeforeApply
            ? MeasureReplacementCandidateWidth(
                textNode,
                replacementText)
            : (ushort)0,
        allowWidthGrowth);

    textNode->TextFlags |= TextFlags.WordWrap
                           | TextFlags.MultiLine
                           | TextFlags.AutoAdjustNodeSize;

    if (resolvedWrapWidth > 0)
    {
      textNode->SetWidth(resolvedWrapWidth);
    }

    textNode->SetText(replacementText);
    textNode->ResizeNodeForCurrentText();

    if (resolvedWrapWidth > 0)
    {
      var resolvedPostApplyWidth = ResolvePostApplyTextNodeWidth(
          resolvedWrapWidth,
          textNode->GetWidth(),
          measureReplacementWidthBeforeApply);
      if (resolvedPostApplyWidth > 0 &&
          textNode->GetWidth() != resolvedPostApplyWidth)
      {
        textNode->SetWidth(resolvedPostApplyWidth);
      }
    }

    TryMeasureTextNode(textNode, out var width, out var height);
    if (resolvedWrapWidth > 0)
    {
      width = ResolveReplacementContainerWidth(
          resolvedWrapWidth,
          width,
          measureReplacementWidthBeforeApply);
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
  /// <param name="restoreHorizontalCentering">
  ///     Whether horizontally centered text should be re-centered after width
  ///     growth changes the immediate wrapper width.
  /// </param>
  /// <param name="minimumSecondaryHorizontalPadding">
  ///     The minimum horizontal padding to preserve inside the secondary
  ///     container when it grows.
  /// </param>
  /// <param name="minimumSecondaryVerticalPadding">
  ///     The minimum vertical padding to preserve inside the secondary
  ///     container when it grows.
  /// </param>
  public static void ResizeFromSnapshot(
      NativeTextNodeLayoutSnapshot snapshot,
      NativeTextNodeResizeResult resizeResult,
      AtkResNode* primaryContainerNode = null,
      AtkResNode* secondaryContainerNode = null,
      AtkResNode* anchoredXNode = null,
      bool allowWidthGrowth = false,
      bool restoreHorizontalCentering = true,
      int minimumSecondaryHorizontalPadding = 0,
      int minimumSecondaryVerticalPadding = 0)
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

    var hasDetachedPrimaryContainer =
        snapshot.DetachedPrimaryContainerAddress != 0 &&
        primaryContainerNode != null;
    if (hasDetachedPrimaryContainer)
    {
      if (allowWidthGrowth && snapshot.DetachedPrimaryContainerWidth > 0)
      {
        var synchronizedWidth = ResolveSynchronizedContainerExtent(
            snapshot.DetachedPrimaryContainerWidth,
            snapshot.SecondaryContainerWidth,
            snapshot.TextWidth,
            resizeResult.Width,
            minimumSecondaryHorizontalPadding);
        if (synchronizedWidth > 0)
        {
          primaryContainerNode->SetWidth(synchronizedWidth);
          childWidth = synchronizedWidth;
        }
      }

      if (snapshot.DetachedPrimaryContainerHeight > 0)
      {
        var synchronizedHeight = ResolveSynchronizedContainerExtent(
            snapshot.DetachedPrimaryContainerHeight,
            snapshot.SecondaryContainerHeight,
            snapshot.TextHeight,
            resizeResult.Height,
            minimumSecondaryVerticalPadding);
        if (synchronizedHeight > 0)
        {
          primaryContainerNode->SetHeight(synchronizedHeight);
          childHeight = synchronizedHeight;
        }
      }
    }

    if (secondaryContainerNode != null)
    {
      if (allowWidthGrowth && snapshot.SecondaryContainerWidth > 0)
      {
        var secondaryWidth = hasDetachedPrimaryContainer
            ? ResolveSynchronizedContainerExtent(
                snapshot.DetachedPrimaryContainerWidth,
                snapshot.SecondaryContainerWidth,
                snapshot.TextWidth,
                resizeResult.Width,
                minimumSecondaryHorizontalPadding)
            : ResolveExpandedContainerExtent(
                snapshot.SecondaryContainerWidth,
                snapshot.TextWidth,
                resizeResult.Width,
                minimumSecondaryHorizontalPadding);
        secondaryContainerNode->SetWidth((ushort)Math.Min(ushort.MaxValue, secondaryWidth));
      }

      if (snapshot.SecondaryContainerHeight > 0)
      {
        var secondaryHeight = hasDetachedPrimaryContainer
            ? ResolveSynchronizedContainerExtent(
                snapshot.DetachedPrimaryContainerHeight,
                snapshot.SecondaryContainerHeight,
                snapshot.TextHeight,
                resizeResult.Height,
                minimumSecondaryVerticalPadding)
            : ResolveExpandedContainerExtent(
                snapshot.SecondaryContainerHeight,
                snapshot.TextHeight,
                resizeResult.Height,
                minimumSecondaryVerticalPadding);
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

    if (secondaryContainerNode != null &&
        anchoredXNode != null &&
        snapshot.AnchoredXWidth > 0)
    {
      var minimumSecondaryWidth = ResolveMinimumSecondaryWidthForAnchoredNode(
          secondaryContainerNode->GetXShort(),
          secondaryContainerNode->GetWidth(),
          anchoredXNode->GetXShort(),
          anchoredXNode->GetWidth(),
          snapshot.AnchoredXRightPaddingFromSecondary);
      if (minimumSecondaryWidth > secondaryContainerNode->GetWidth())
      {
        secondaryContainerNode->SetWidth(minimumSecondaryWidth);
      }
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

    if (snapshot.DetachedPrimaryContainerAddress != 0)
    {
      var detachedPrimaryContainerNode = (AtkResNode*)snapshot.DetachedPrimaryContainerAddress;
      if (detachedPrimaryContainerNode != null)
      {
        if (snapshot.DetachedPrimaryContainerWidth > 0)
        {
          detachedPrimaryContainerNode->SetWidth(snapshot.DetachedPrimaryContainerWidth);
        }

        if (snapshot.DetachedPrimaryContainerHeight > 0)
        {
          detachedPrimaryContainerNode->SetHeight(snapshot.DetachedPrimaryContainerHeight);
        }

        if (restorePositions)
        {
          detachedPrimaryContainerNode->SetXShort(snapshot.DetachedPrimaryContainerOriginalX);
          detachedPrimaryContainerNode->SetYShort(snapshot.DetachedPrimaryContainerOriginalY);
        }
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
  /// <param name="restoreHorizontalCentering">
  ///     Whether horizontally centered text should be re-centered after reflow.
  /// </param>
  /// <param name="additionalWrapWidth">
  ///     Additional width to add to the preserved wrap width before applying
  ///     the translated text.
  /// </param>
  /// <param name="minimumSecondaryHorizontalPadding">
  ///     The minimum horizontal padding to preserve inside the secondary
  ///     background when it grows.
  /// </param>
  /// <param name="minimumSecondaryVerticalPadding">
  ///     The minimum vertical padding to preserve inside the secondary
  ///     background when it grows.
  /// </param>
  /// <param name="measureReplacementWidthBeforeApply">
  ///     Whether the candidate replacement text should be measured before
  ///     native apply so width growth can use its draw width.
  /// </param>
  public static NativeTextNodeLayoutSnapshot? ApplyTextReplacementWithInferredReflow(
      AtkUnitBase* addon,
      AtkTextNode* textNode,
      string replacementText,
      bool allowWidthGrowth = false,
      bool restoreHorizontalCentering = true,
      ushort additionalWrapWidth = 0,
      int minimumSecondaryHorizontalPadding = 0,
      int minimumSecondaryVerticalPadding = 0,
      bool measureReplacementWidthBeforeApply = false)
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
        preferredWrapWidth,
        allowWidthGrowth,
        measureReplacementWidthBeforeApply);
    ResizeFromSnapshot(
        snapshot,
        resizeResult,
        containerNode,
        backgroundResNode,
        allowWidthGrowth: allowWidthGrowth,
        restoreHorizontalCentering: restoreHorizontalCentering,
        minimumSecondaryHorizontalPadding: minimumSecondaryHorizontalPadding,
        minimumSecondaryVerticalPadding: minimumSecondaryVerticalPadding);
    return snapshot;
  }

  /// <summary>
  ///     Resolves the wrap width that should be applied to one replacement
  ///     text, preserving the historical width by default and only widening to
  ///     the candidate draw width when the caller explicitly allows it.
  /// </summary>
  /// <param name="preferredWrapWidth">The historical wrap width to preserve.</param>
  /// <param name="candidateDrawWidth">
  ///     The measured draw width of the replacement text before it is applied
  ///     to the node.
  /// </param>
  /// <param name="allowWidthGrowth">
  ///     Whether horizontal growth is allowed for this replacement.
  /// </param>
  /// <returns>The wrap width that should be assigned to the node.</returns>
  public static ushort ResolveReplacementWrapWidth(
      ushort preferredWrapWidth,
      ushort candidateDrawWidth,
      bool allowWidthGrowth)
  {
    if (allowWidthGrowth)
    {
      return (ushort)Math.Min(
          ushort.MaxValue,
          Math.Max(
              preferredWrapWidth,
              candidateDrawWidth));
    }

    if (preferredWrapWidth > 0)
    {
      return preferredWrapWidth;
    }

    return candidateDrawWidth;
  }

  /// <summary>
  ///     Resolves the effective container width for a replacement after the
  ///     live node has been measured.
  /// </summary>
  /// <param name="resolvedWrapWidth">
  ///     The wrap width assigned before applying the replacement.
  /// </param>
  /// <param name="measuredWidth">
  ///     The post-apply measured width reported by the live text node.
  /// </param>
  /// <param name="preferMeasuredOverflow">
  ///     Whether callers should preserve measured overflow when it exceeds the
  ///     assigned wrap width.
  /// </param>
  /// <returns>The width that should drive container resizing.</returns>
  public static ushort ResolveReplacementContainerWidth(
      ushort resolvedWrapWidth,
      ushort measuredWidth,
      bool preferMeasuredOverflow)
  {
    if (resolvedWrapWidth == 0)
    {
      return measuredWidth;
    }

    if (!preferMeasuredOverflow || measuredWidth == 0)
    {
      return resolvedWrapWidth;
    }

    return (ushort)Math.Min(
        ushort.MaxValue,
        Math.Max(
            resolvedWrapWidth,
            measuredWidth));
  }

  /// <summary>
  ///     Resolves the width that should remain on the live text node after the
  ///     game has reflowed the replacement text.
  /// </summary>
  /// <param name="resolvedWrapWidth">
  ///     The wrap width assigned before applying the replacement.
  /// </param>
  /// <param name="postApplyNodeWidth">
  ///     The width reported by the live node immediately after native reflow.
  /// </param>
  /// <param name="preferMeasuredOverflow">
  ///     Whether callers should preserve native width growth when it exceeds
  ///     the assigned wrap width.
  /// </param>
  /// <returns>The width that should remain on the text node.</returns>
  public static ushort ResolvePostApplyTextNodeWidth(
      ushort resolvedWrapWidth,
      ushort postApplyNodeWidth,
      bool preferMeasuredOverflow)
  {
    return ResolveReplacementContainerWidth(
        resolvedWrapWidth,
        postApplyNodeWidth,
        preferMeasuredOverflow);
  }

  /// <summary>
  ///     Resolves the effective measured text extent by combining the live node
  ///     size with the text draw size when wrapped text still reports a stale
  ///     one-line node height.
  /// </summary>
  /// <param name="liveWidth">The current live node width.</param>
  /// <param name="liveHeight">The current live node height.</param>
  /// <param name="drawWidth">The measured text draw width.</param>
  /// <param name="drawHeight">The measured text draw height.</param>
  /// <param name="textFlags">The live text flags.</param>
  /// <returns>The effective measured width and height.</returns>
  public static NativeTextNodeResizeResult ResolveMeasuredTextExtent(
      ushort liveWidth,
      ushort liveHeight,
      ushort drawWidth,
      ushort drawHeight,
      TextFlags textFlags)
  {
    var width = liveWidth;
    var height = liveHeight;
    var prefersDrawSize =
        (textFlags & (TextFlags.WordWrap |
                      TextFlags.MultiLine |
                      TextFlags.AutoAdjustNodeSize)) != 0;

    if (prefersDrawSize)
    {
      width = Math.Max(width, drawWidth);
      height = Math.Max(height, drawHeight);
    }

    if (width == 0)
    {
      width = drawWidth;
    }

    if (height == 0)
    {
      height = drawHeight;
    }

    return new NativeTextNodeResizeResult(width, height);
  }

  /// <summary>
  ///     Measures the candidate replacement text width using the node's live
  ///     font and style fields without permanently mutating the visible text or
  ///     preserving the node's current wrap clamp.
  /// </summary>
  /// <param name="textNode">The live text node whose style should be reused.</param>
  /// <param name="replacementText">The candidate replacement text.</param>
  /// <returns>The measured candidate width.</returns>
  private static ushort MeasureReplacementCandidateWidth(
      AtkTextNode* textNode,
      string replacementText)
  {
    if (textNode == null ||
        string.IsNullOrEmpty(replacementText))
    {
      return 0;
    }

    var originalTextFlags = textNode->TextFlags;
    try
    {
      textNode->TextFlags &= ~(TextFlags.WordWrap | TextFlags.MultiLine);
      ushort measuredWidth = 0;
      ushort measuredHeight = 0;
      var utf8Length = Encoding.UTF8.GetByteCount(replacementText);
      Span<byte> utf8Bytes =
          utf8Length <= 511 ? stackalloc byte[512] : new byte[utf8Length + 1];
      Encoding.UTF8.GetBytes(replacementText, utf8Bytes);
      utf8Bytes[utf8Length] = 0;

      fixed (byte* utf8Pointer = utf8Bytes)
      {
        textNode->GetTextDrawSize(
            &measuredWidth,
            &measuredHeight,
            utf8Pointer,
            0,
            -1,
            true);
      }

      return measuredWidth;
    }
    finally
    {
      textNode->TextFlags = originalTextFlags;
    }
  }

  /// <summary>
  ///     Resolves the expanded extent for a background or wrapper node while
  ///     preserving its historical padding and enforcing an optional minimum
  ///     cushion for dense tooltip-style surfaces.
  /// </summary>
  /// <param name="currentContainerExtent">The current container extent.</param>
  /// <param name="currentTextExtent">The current text extent.</param>
  /// <param name="measuredTextExtent">The measured text extent after reflow.</param>
  /// <param name="minimumPadding">The minimum padding to preserve.</param>
  /// <returns>The expanded container extent.</returns>
  public static ushort ResolveExpandedContainerExtent(
      ushort currentContainerExtent,
      ushort currentTextExtent,
      ushort measuredTextExtent,
      int minimumPadding)
  {
    if (currentContainerExtent == 0)
    {
      return 0;
    }

    var preservedPadding = Math.Max(
        0,
        currentContainerExtent - currentTextExtent);
    var effectivePadding = Math.Max(
        preservedPadding,
        Math.Max(0, minimumPadding));
    var resolvedExtent = Math.Max(
        currentContainerExtent,
        measuredTextExtent + effectivePadding);
    return (ushort)Math.Min(ushort.MaxValue, Math.Max(1, resolvedExtent));
  }

  /// <summary>
  ///     Resolves one shared extent for detached tooltip-style containers that
  ///     must stay visually aligned even when the text node does not live under
  ///     the same native wrapper chain.
  /// </summary>
  /// <param name="primaryContainerExtent">The primary container extent.</param>
  /// <param name="secondaryContainerExtent">The secondary container extent.</param>
  /// <param name="currentTextExtent">The current text extent.</param>
  /// <param name="measuredTextExtent">The measured text extent after reflow.</param>
  /// <param name="minimumSecondaryPadding">
  ///     The explicit padding that should remain inside the synchronized
  ///     tooltip-style background.
  /// </param>
  /// <returns>The synchronized extent shared by both containers.</returns>
  public static ushort ResolveSynchronizedContainerExtent(
      ushort primaryContainerExtent,
      ushort secondaryContainerExtent,
      ushort currentTextExtent,
      ushort measuredTextExtent,
      int minimumSecondaryPadding)
  {
    var baseExtent = Math.Max(
        primaryContainerExtent,
        secondaryContainerExtent);
    var effectiveTextExtent = Math.Max(
        currentTextExtent,
        measuredTextExtent);
    var resolvedExtent = Math.Max(
        baseExtent,
        effectiveTextExtent + Math.Max(0, minimumSecondaryPadding));
    if (resolvedExtent <= 0)
    {
      return 0;
    }

    return (ushort)Math.Min(ushort.MaxValue, Math.Max(1, resolvedExtent));
  }

  /// <summary>
  ///     Resolves the minimum secondary-container width required to keep an
  ///     anchored sibling node covered by the same background after reflow.
  /// </summary>
  /// <param name="secondaryContainerX">
  ///     The live X position of the secondary container.
  /// </param>
  /// <param name="currentSecondaryWidth">
  ///     The current width of the secondary container before adjustment.
  /// </param>
  /// <param name="anchoredNodeX">
  ///     The live X position of the anchored sibling node.
  /// </param>
  /// <param name="anchoredNodeWidth">The current width of the anchored node.</param>
  /// <param name="preferredRightPadding">
  ///     The original right padding between the secondary container and the
  ///     anchored node. Negative values are treated as zero so coverage still
  ///     extends to the anchored node edge.
  /// </param>
  /// <returns>
  ///     The minimum width that keeps the anchored node inside the secondary
  ///     background.
  /// </returns>
  public static ushort ResolveMinimumSecondaryWidthForAnchoredNode(
      short secondaryContainerX,
      ushort currentSecondaryWidth,
      short anchoredNodeX,
      ushort anchoredNodeWidth,
      int preferredRightPadding)
  {
    var secondaryLeft = (int)secondaryContainerX;
    var currentRight = secondaryLeft + currentSecondaryWidth;
    var anchoredRight = anchoredNodeX + anchoredNodeWidth + Math.Max(0, preferredRightPadding);
    if (anchoredRight <= currentRight)
    {
      return currentSecondaryWidth;
    }

    var requiredWidth = Math.Max(
        currentSecondaryWidth,
        anchoredRight - secondaryLeft);
    return (ushort)Math.Min(ushort.MaxValue, requiredWidth);
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

    var liveWidth = textNode->GetWidth();
    var liveHeight = textNode->GetHeight();
    ushort measuredWidth = 0;
    ushort measuredHeight = 0;
    var shouldMeasureDrawSize =
        liveWidth == 0 ||
        liveHeight == 0 ||
        (textNode->TextFlags & (TextFlags.WordWrap |
                                TextFlags.MultiLine |
                                TextFlags.AutoAdjustNodeSize)) != 0;
    if (shouldMeasureDrawSize)
    {
      textNode->GetTextDrawSize(&measuredWidth, &measuredHeight);
    }

    var resolvedExtent = ResolveMeasuredTextExtent(
        liveWidth,
        liveHeight,
        measuredWidth,
        measuredHeight,
        textNode->TextFlags);
    width = resolvedExtent.Width;
    height = resolvedExtent.Height;
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
  ///     Captures the primary container separately when it is not part of the
  ///     text node's parent chain, which is the case for simple tooltip
  ///     addons where the root background and text node are siblings.
  /// </summary>
  /// <param name="snapshot">The snapshot receiving detached container data.</param>
  /// <param name="primaryContainerNode">The resolved primary container node.</param>
  private static void CaptureDetachedPrimaryContainer(
      NativeTextNodeLayoutSnapshot snapshot,
      AtkResNode* primaryContainerNode)
  {
    if (snapshot == null || primaryContainerNode == null)
    {
      return;
    }

    foreach (var ancestorSnapshot in snapshot.AncestorChain)
    {
      if (ancestorSnapshot.NodeAddress == (nint)primaryContainerNode)
      {
        return;
      }
    }

    snapshot.DetachedPrimaryContainerAddress = (nint)primaryContainerNode;
    snapshot.DetachedPrimaryContainerWidth = primaryContainerNode->GetWidth();
    snapshot.DetachedPrimaryContainerHeight = primaryContainerNode->GetHeight();
    snapshot.DetachedPrimaryContainerOriginalX = primaryContainerNode->GetXShort();
    snapshot.DetachedPrimaryContainerOriginalY = primaryContainerNode->GetYShort();
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
  ///     Gets or sets the native address of one detached primary container
  ///     node that must be restored separately from the text wrapper chain.
  /// </summary>
  public nint DetachedPrimaryContainerAddress { get; set; }

  /// <summary>
  ///     Gets or sets the detached primary container width.
  /// </summary>
  public ushort DetachedPrimaryContainerWidth { get; set; }

  /// <summary>
  ///     Gets or sets the detached primary container height.
  /// </summary>
  public ushort DetachedPrimaryContainerHeight { get; set; }

  /// <summary>
  ///     Gets or sets the original X position of the detached primary
  ///     container.
  /// </summary>
  public short DetachedPrimaryContainerOriginalX { get; set; }

  /// <summary>
  ///     Gets or sets the original Y position of the detached primary
  ///     container.
  /// </summary>
  public short DetachedPrimaryContainerOriginalY { get; set; }

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
  ///     Gets or sets the original width of the anchored sibling node.
  /// </summary>
  public ushort AnchoredXWidth { get; set; }

  /// <summary>
  ///     Gets or sets the original right padding between the anchored sibling
  ///     node and the secondary container.
  /// </summary>
  public int AnchoredXRightPaddingFromSecondary { get; set; }

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
