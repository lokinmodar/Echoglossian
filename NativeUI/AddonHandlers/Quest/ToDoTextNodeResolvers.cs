// <copyright file="ToDoTextNodeResolvers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text.RegularExpressions;
using Echoglossian.NativeUI.AddonHandlers.Toasts;

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Resolves the readable text rows displayed by the dedicated ToDo addon.
/// </summary>
internal static unsafe partial class ToDoTextNodeResolvers
{
    /// <summary>
    ///     Resolves visible ToDo text in display order, keeping countdown text
    ///     in the payload while excluding it from translation identity.
    /// </summary>
    /// <param name="addon">The live ToDo addon.</param>
    /// <returns>The visible text rows in display order.</returns>
    public static IReadOnlyList<ToDoCapturedText> ResolveVisibleTexts(
        AtkUnitBase* addon)
    {
        return ResolveVisibleTextNodes(addon)
            .Select(node => new ToDoCapturedText(
                node.NodeKey,
                node.NodeId,
                node.Text,
                node.IsTimerNode))
            .ToArray();
    }

    /// <summary>
    ///     Resolves visible ToDo text nodes with an identity that remains unique
    ///     when repeated component node ids occur in the tree.
    /// </summary>
    /// <param name="addon">The live ToDo addon.</param>
    /// <returns>The readable visible text nodes in display order.</returns>
    public static IReadOnlyList<ToDoResolvedTextNode> ResolveVisibleTextNodes(
        AtkUnitBase* addon)
    {
        Dictionary<int, int> ordinalsByNodeId = [];
        List<ToDoResolvedTextNode> visibleNodes = [];
        foreach (var textNodeAddress in ResolveVisibleTextNodeAddresses(addon))
        {
            var textNode = (AtkTextNode*)textNodeAddress;
            if (textNode == null)
            {
                continue;
            }

            var nodeId = (int)textNode->AtkResNode.NodeId;
            var nodeOrdinal = ordinalsByNodeId.GetValueOrDefault(nodeId);
            ordinalsByNodeId[nodeId] = nodeOrdinal + 1;
            var text = ReadTextNode(textNode);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            visibleNodes.Add(new ToDoResolvedTextNode(
                $"{nodeId}:{nodeOrdinal}",
                nodeId,
                text,
                IsTimerText(text),
                textNodeAddress));
        }

        return visibleNodes;
    }

    /// <summary>
    ///     Resolves the visible ToDo text node addresses in display order.
    /// </summary>
    /// <param name="addon">The live ToDo addon.</param>
    /// <returns>The readable visible text-node addresses.</returns>
    public static IReadOnlyList<nint> ResolveVisibleTextNodeAddresses(
        AtkUnitBase* addon)
    {
        return AddonTextNodeResolvers.ResolveReadableTextNodes(addon);
    }

    /// <summary>
    ///     Reads the most useful text representation from one native text node.
    /// </summary>
    /// <param name="textNode">The native text node.</param>
    /// <returns>The readable text, or an empty string.</returns>
    public static string ReadTextNode(AtkTextNode* textNode)
    {
        if (textNode == null)
        {
            return string.Empty;
        }

        var text = textNode->NodeText.ToString();
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        try
        {
            return textNode->OriginalTextPointer
                .AsReadOnlySeStringSpan()
                .ExtractText();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    ///     Determines whether a readable ToDo row is the volatile countdown.
    /// </summary>
    /// <param name="text">The visible text.</param>
    /// <returns><c>true</c> when the text is a countdown value.</returns>
    internal static bool IsTimerText(string text)
    {
        return TimerTextPattern().IsMatch(text);
    }

    [GeneratedRegex(@"^\s*(?:\d{1,3}:\d{2}|\d{1,2}:\d{1,2}:\d{2})\s*$")]
    private static partial Regex TimerTextPattern();
}

/// <summary>
///     Represents one visible ToDo node with a structural identity.
/// </summary>
/// <param name="NodeKey">The node id plus traversal ordinal.</param>
/// <param name="NodeId">The native node id.</param>
/// <param name="Text">The readable node text.</param>
/// <param name="IsTimerNode">Whether the node displays volatile timer text.</param>
/// <param name="Address">The live native text-node address.</param>
internal readonly record struct ToDoResolvedTextNode(
    string NodeKey,
    int NodeId,
    string Text,
    bool IsTimerNode,
    nint Address);
