// <copyright file="NativeTextFlowReflowHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Provides shared snapshot, reflow, and restore helpers for native UI
///     text flows where translated text can grow beyond the original English
///     layout budget.
/// </summary>
internal static unsafe class NativeTextFlowReflowHelper
{
  /// <summary>
  ///     Resolves the visual wrapper node address for one text node inside a
  ///     given flow root.
  /// </summary>
  /// <param name="flowRoot">The visual flow root that owns the blocks.</param>
  /// <param name="textNode">The text node whose wrapper should be resolved.</param>
  /// <returns>The resolved wrapper-node address.</returns>
  internal static nint ResolveFlowWrapperNodeAddress(
      AtkResNode* flowRoot,
      AtkTextNode* textNode)
  {
    return (nint)ResolveWrapperNode(flowRoot, textNode);
  }

  /// <summary>
  ///     Captures the ordered flow-block snapshots for the supplied text nodes.
  /// </summary>
  /// <param name="flowRoot">The visual flow root that owns the blocks.</param>
  /// <param name="textNodeAddresses">The text nodes that participate in the flow.</param>
  /// <returns>The captured ordered flow blocks.</returns>
  internal static IReadOnlyList<NativeTextFlowBlockSnapshot>
      CaptureOrderedFlowBlocks(
          AtkResNode* flowRoot,
          IEnumerable<nint> textNodeAddresses)
  {
    List<NativeTextFlowBlockSnapshot> blocks = [];

    foreach (var textNodeAddress in textNodeAddresses)
    {
      var textNode = (AtkTextNode*)textNodeAddress;
      if (textNode == null)
      {
        continue;
      }

      var wrapperNode = ResolveWrapperNode(flowRoot, textNode);
      var textOffset =
          wrapperNode == (AtkResNode*)textNode
              ? Vector2.Zero
              : CalculateNodeOffsetToAncestor(
                  (AtkResNode*)textNode,
                  wrapperNode);
      var textOffsetX = ClampToShort(textOffset.X);
      var textOffsetY = ClampToShort(textOffset.Y);
      var textHeight = textNode->GetHeight();
      var wrapperHeight = wrapperNode->GetHeight();
      var bottomPadding = wrapperNode == (AtkResNode*)textNode
          ? (short)0
          : ClampToShort(
              Math.Max(
                  0f,
                  wrapperHeight - (textOffsetY + textHeight)));

      blocks.Add(
          new NativeTextFlowBlockSnapshot(
              (nint)wrapperNode,
              ClampToShort(wrapperNode->X),
              ClampToShort(wrapperNode->Y),
              wrapperNode->GetWidth(),
              wrapperHeight,
              (nint)textNode,
              textOffsetX,
              textOffsetY,
              textNode->GetWidth(),
              textHeight,
              textNode->TextFlags,
              textNode->FontSize,
              bottomPadding));
    }

    blocks.Sort(
        static (left, right) =>
        {
          var verticalComparison = left.WrapperY.CompareTo(right.WrapperY);
          return verticalComparison != 0
              ? verticalComparison
              : left.WrapperX.CompareTo(right.WrapperX);
        });

    return blocks;
  }

  /// <summary>
  ///     Captures one container snapshot for a text flow.
  /// </summary>
  /// <param name="containerNode">The container node to snapshot.</param>
  /// <param name="flowOriginY">
  ///     The vertical origin of the flow inside the container.
  /// </param>
  /// <param name="flowBlocks">The captured flow blocks.</param>
  /// <returns>The captured container snapshot, or <see langword="null" />.</returns>
  internal static NativeTextFlowContainerSnapshot? CaptureContainerSnapshot(
      AtkResNode* containerNode,
      short flowOriginY,
      IReadOnlyList<NativeTextFlowBlockSnapshot> flowBlocks)
  {
    if (containerNode == null)
    {
      return null;
    }

    var lastBottom = flowBlocks.Count == 0
        ? flowOriginY
        : flowOriginY + flowBlocks.Max(
            static block => block.WrapperY + block.WrapperHeight);
    var bottomPadding = ClampToShort(
        Math.Max(
            0f,
            containerNode->GetHeight() - lastBottom));

    return new NativeTextFlowContainerSnapshot(
        (nint)containerNode,
        flowOriginY,
        containerNode->GetHeight(),
        bottomPadding);
  }

  /// <summary>
  ///     Applies translated text to an ordered native text flow and reflows
  ///     later blocks by cumulative height delta.
  /// </summary>
  /// <param name="flowBlocks">The ordered flow blocks.</param>
  /// <param name="translatedTextsByTextNode">
  ///     The translated text keyed by text-node address.
  /// </param>
  /// <param name="applyText">
  ///     Delegate used to apply translated text with the desired presentation.
  /// </param>
  /// <param name="containerSnapshots">
  ///     The containers whose heights should grow with the flow.
  /// </param>
  internal static void ApplyVerticalTextFlow(
      IReadOnlyList<NativeTextFlowBlockSnapshot> flowBlocks,
      IReadOnlyDictionary<nint, string> translatedTextsByTextNode,
      NativeTextFlowApplyDelegate applyText,
      IReadOnlyList<NativeTextFlowContainerSnapshot> containerSnapshots)
  {
    if (flowBlocks.Count == 0)
    {
      return;
    }

    Dictionary<nint, ushort> desiredWrapperHeights = [];

    foreach (var block in flowBlocks)
    {
      var wrapperNode = (AtkResNode*)block.WrapperNodeAddress;
      var textNode = (AtkTextNode*)block.TextNodeAddress;
      if (wrapperNode == null || textNode == null)
      {
        continue;
      }

      wrapperNode->SetPositionShort(
          block.WrapperX,
          block.WrapperY);
      wrapperNode->SetHeight(block.WrapperHeight);

      if (block.WrapperNodeAddress != block.TextNodeAddress)
      {
        textNode->SetPositionShort(
            block.TextOffsetX,
            block.TextOffsetY);
      }

      if (translatedTextsByTextNode.TryGetValue(
              block.TextNodeAddress,
              out var translatedText))
      {
        applyText(
            textNode,
            block.TextWidth,
            block.TextFlags,
            block.FontSize,
            translatedText ?? string.Empty);
      }

      var desiredWrapperHeight = block.WrapperNodeAddress == block.TextNodeAddress
          ? textNode->GetHeight()
          : Math.Max(
              (double)block.WrapperHeight,
              block.TextOffsetY + textNode->GetHeight() + block.BottomPadding);
      var clampedWrapperHeight = (ushort)Math.Clamp(
          Math.Ceiling(desiredWrapperHeight),
          ushort.MinValue,
          ushort.MaxValue);
      desiredWrapperHeights[block.TextNodeAddress] = clampedWrapperHeight;
    }

    var layoutPlan = CalculateVerticalLayoutPlan(
        flowBlocks,
        desiredWrapperHeights,
        containerSnapshots);
    foreach (var blockPlan in layoutPlan.BlockPlans)
    {
      var wrapperNode = (AtkResNode*)blockPlan.WrapperNodeAddress;
      if (wrapperNode == null)
      {
        continue;
      }

      wrapperNode->SetPositionShort(
          blockPlan.WrapperX,
          blockPlan.WrapperY);
      wrapperNode->SetHeight(blockPlan.WrapperHeight);
    }

    foreach (var containerPlan in layoutPlan.ContainerPlans)
    {
      var containerNode = (AtkResNode*)containerPlan.ContainerNodeAddress;
      if (containerNode == null)
      {
        continue;
      }

      containerNode->SetHeight(containerPlan.Height);
    }
  }

  /// <summary>
  ///     Restores an ordered native text flow to its original layout and text.
  /// </summary>
  /// <param name="flowBlocks">The ordered flow blocks.</param>
  /// <param name="originalTextsByTextNode">
  ///     The original text keyed by text-node address.
  /// </param>
  /// <param name="restoreText">
  ///     Delegate used to restore the original text presentation.
  /// </param>
  /// <param name="containerSnapshots">
  ///     The container snapshots captured before native mutation.
  /// </param>
  internal static void RestoreVerticalTextFlow(
      IReadOnlyList<NativeTextFlowBlockSnapshot> flowBlocks,
      IReadOnlyDictionary<nint, string> originalTextsByTextNode,
      NativeTextFlowApplyDelegate restoreText,
      IReadOnlyList<NativeTextFlowContainerSnapshot> containerSnapshots)
  {
    foreach (var block in flowBlocks)
    {
      var wrapperNode = (AtkResNode*)block.WrapperNodeAddress;
      var textNode = (AtkTextNode*)block.TextNodeAddress;
      if (wrapperNode == null || textNode == null)
      {
        continue;
      }

      wrapperNode->SetPositionShort(block.WrapperX, block.WrapperY);
      wrapperNode->SetHeight(block.WrapperHeight);

      if (block.WrapperNodeAddress != block.TextNodeAddress)
      {
        textNode->SetPositionShort(
            block.TextOffsetX,
            block.TextOffsetY);
      }

      if (originalTextsByTextNode.TryGetValue(
              block.TextNodeAddress,
              out var originalText))
      {
        restoreText(
            textNode,
            block.TextWidth,
            block.TextFlags,
            block.FontSize,
            originalText ?? string.Empty);
      }
    }

    foreach (var containerSnapshot in containerSnapshots)
    {
      var containerNode = (AtkResNode*)containerSnapshot.ContainerNodeAddress;
      if (containerNode == null)
      {
        continue;
      }

      containerNode->SetHeight(containerSnapshot.Height);
    }
  }

  /// <summary>
  ///     Resolves the nearest visual wrapper node for a text node inside a
  ///     given flow root.
  /// </summary>
  /// <param name="flowRoot">The flow root owning the text flow.</param>
  /// <param name="textNode">The text node being wrapped.</param>
  /// <returns>The resolved wrapper node.</returns>
  private static AtkResNode* ResolveWrapperNode(
      AtkResNode* flowRoot,
      AtkTextNode* textNode)
  {
    if (flowRoot == null || textNode == null)
    {
      return (AtkResNode*)textNode;
    }

    var textResNode = (AtkResNode*)textNode;
    var parentNode = textResNode->ParentNode;
    if (parentNode == null)
    {
      return textResNode;
    }

    if (parentNode == flowRoot)
    {
      return textResNode;
    }

    return IsDescendantOf(parentNode, flowRoot)
        ? parentNode
        : textResNode;
  }

  /// <summary>
  ///     Calculates the local offset between one node and one ancestor node.
  /// </summary>
  /// <param name="node">The descendant node.</param>
  /// <param name="ancestorNode">The ancestor node.</param>
  /// <returns>The accumulated local offset from the ancestor to the node.</returns>
  private static Vector2 CalculateNodeOffsetToAncestor(
      AtkResNode* node,
      AtkResNode* ancestorNode)
  {
    var currentNode = node;
    var offset = Vector2.Zero;
    while (currentNode != null && currentNode != ancestorNode)
    {
      offset.X += currentNode->X;
      offset.Y += currentNode->Y;
      currentNode = currentNode->ParentNode;
    }

    return offset;
  }

  /// <summary>
  ///     Gets whether one node belongs to the subtree rooted at the supplied
  ///     ancestor node.
  /// </summary>
  /// <param name="node">The node to test.</param>
  /// <param name="ancestorNode">The ancestor node.</param>
  /// <returns><c>true</c> when the node belongs to the ancestor subtree.</returns>
  private static bool IsDescendantOf(
      AtkResNode* node,
      AtkResNode* ancestorNode)
  {
    var currentNode = node;
    while (currentNode != null)
    {
      if (currentNode == ancestorNode)
      {
        return true;
      }

      currentNode = currentNode->ParentNode;
    }

    return false;
  }

  /// <summary>
  ///     Calculates the wrapper positions and container heights implied by one
  ///     measured vertical text flow.
  /// </summary>
  /// <param name="flowBlocks">The ordered original flow blocks.</param>
  /// <param name="desiredWrapperHeights">
  ///     The desired wrapper heights keyed by text-node address after
  ///     translated measurement.
  /// </param>
  /// <param name="containerSnapshots">The captured container snapshots.</param>
  /// <returns>The calculated layout plan.</returns>
  internal static NativeTextFlowLayoutPlan CalculateVerticalLayoutPlan(
      IReadOnlyList<NativeTextFlowBlockSnapshot> flowBlocks,
      IReadOnlyDictionary<nint, ushort> desiredWrapperHeights,
      IReadOnlyList<NativeTextFlowContainerSnapshot> containerSnapshots)
  {
    List<NativeTextFlowBlockLayoutPlan> blockPlans = [];
    var cumulativeDelta = 0f;
    var finalBottom = 0f;

    foreach (var block in flowBlocks)
    {
      var wrapperHeight = desiredWrapperHeights.TryGetValue(
          block.TextNodeAddress,
          out var desiredWrapperHeight)
          ? desiredWrapperHeight
          : block.WrapperHeight;
      var wrapperY = block.WrapperY + cumulativeDelta;
      blockPlans.Add(
          new NativeTextFlowBlockLayoutPlan(
              block.WrapperNodeAddress,
              block.WrapperX,
              ClampToShort(wrapperY),
              wrapperHeight));
      finalBottom = Math.Max(
          finalBottom,
          wrapperY + wrapperHeight);
      cumulativeDelta += wrapperHeight - block.WrapperHeight;
    }

    List<NativeTextFlowContainerLayoutPlan> containerPlans = [];
    foreach (var containerSnapshot in containerSnapshots)
    {
      var desiredHeight = Math.Max(
          containerSnapshot.Height,
          containerSnapshot.FlowOriginY + finalBottom + containerSnapshot.BottomPadding);
      containerPlans.Add(
          new NativeTextFlowContainerLayoutPlan(
              containerSnapshot.ContainerNodeAddress,
              (ushort)Math.Clamp(
                  Math.Ceiling(desiredHeight),
                  ushort.MinValue,
                  ushort.MaxValue)));
    }

    return new NativeTextFlowLayoutPlan(
        blockPlans,
        containerPlans);
  }

  /// <summary>
  ///     Clamps one local coordinate to the short range expected by native
  ///     node positioning helpers.
  /// </summary>
  /// <param name="value">The coordinate to clamp.</param>
  /// <returns>The clamped coordinate.</returns>
  private static short ClampToShort(
      float value)
  {
    return (short)Math.Clamp(
        Math.Round(value),
        short.MinValue,
        short.MaxValue);
  }
}

/// <summary>
///     Applies one translated or restored text payload to a native text node
///     using its original presentation metadata.
/// </summary>
/// <param name="textNode">The target text node.</param>
/// <param name="originalWidth">The original text-node width.</param>
/// <param name="originalTextFlags">The original text flags.</param>
/// <param name="originalFontSize">The original font size.</param>
/// <param name="text">The text to render.</param>
internal unsafe delegate void NativeTextFlowApplyDelegate(
    AtkTextNode* textNode,
    ushort originalWidth,
    TextFlags originalTextFlags,
    byte originalFontSize,
    string text);

/// <summary>
///     Captures one reflowable native text block.
/// </summary>
/// <param name="WrapperNodeAddress">The wrapper node address.</param>
/// <param name="WrapperX">The original wrapper X.</param>
/// <param name="WrapperY">The original wrapper Y.</param>
/// <param name="WrapperWidth">The original wrapper width.</param>
/// <param name="WrapperHeight">The original wrapper height.</param>
/// <param name="TextNodeAddress">The inner text-node address.</param>
/// <param name="TextOffsetX">The original text offset X inside the wrapper.</param>
/// <param name="TextOffsetY">The original text offset Y inside the wrapper.</param>
/// <param name="TextWidth">The original text-node width.</param>
/// <param name="TextHeight">The original text-node height.</param>
/// <param name="TextFlags">The original text flags.</param>
/// <param name="FontSize">The original font size.</param>
/// <param name="BottomPadding">
///     The original bottom padding between the text bottom and wrapper bottom.
/// </param>
internal sealed record NativeTextFlowBlockSnapshot(
    nint WrapperNodeAddress,
    short WrapperX,
    short WrapperY,
    ushort WrapperWidth,
    ushort WrapperHeight,
    nint TextNodeAddress,
    short TextOffsetX,
    short TextOffsetY,
    ushort TextWidth,
    ushort TextHeight,
    TextFlags TextFlags,
    byte FontSize,
    short BottomPadding);

/// <summary>
///     Captures one container that should grow together with a native text
///     flow.
/// </summary>
/// <param name="ContainerNodeAddress">The container node address.</param>
/// <param name="FlowOriginY">The flow origin Y inside the container.</param>
/// <param name="Height">The original container height.</param>
/// <param name="BottomPadding">The original bottom padding below the flow.</param>
internal sealed record NativeTextFlowContainerSnapshot(
    nint ContainerNodeAddress,
    short FlowOriginY,
    ushort Height,
    short BottomPadding);

/// <summary>
///     Captures the calculated reflow plan for one native vertical text flow.
/// </summary>
/// <param name="BlockPlans">The ordered wrapper position and size plans.</param>
/// <param name="ContainerPlans">The container height plans.</param>
internal sealed record NativeTextFlowLayoutPlan(
    IReadOnlyList<NativeTextFlowBlockLayoutPlan> BlockPlans,
    IReadOnlyList<NativeTextFlowContainerLayoutPlan> ContainerPlans);

/// <summary>
///     Captures the calculated position and height for one wrapper block after
///     translated reflow.
/// </summary>
/// <param name="WrapperNodeAddress">The wrapper node address.</param>
/// <param name="WrapperX">The wrapper X position.</param>
/// <param name="WrapperY">The wrapper Y position.</param>
/// <param name="WrapperHeight">The wrapper height.</param>
internal sealed record NativeTextFlowBlockLayoutPlan(
    nint WrapperNodeAddress,
    short WrapperX,
    short WrapperY,
    ushort WrapperHeight);

/// <summary>
///     Captures the calculated height for one container after native text
///     reflow.
/// </summary>
/// <param name="ContainerNodeAddress">The container node address.</param>
/// <param name="Height">The calculated container height.</param>
internal sealed record NativeTextFlowContainerLayoutPlan(
    nint ContainerNodeAddress,
    ushort Height);
