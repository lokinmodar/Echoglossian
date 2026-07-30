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
        List<ToDoCapturedText> visibleTexts = [];
        foreach (var textNodeAddress in ResolveVisibleTextNodeAddresses(addon))
        {
            var textNode = (AtkTextNode*)textNodeAddress;
            if (textNode == null)
            {
                continue;
            }

            var text = ReadTextNode(textNode);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            visibleTexts.Add(new ToDoCapturedText(
                (int)textNode->AtkResNode.NodeId,
                text,
                IsTimerText(text)));
        }

        return visibleTexts;
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
    private static bool IsTimerText(string text)
    {
        return TimerTextPattern().IsMatch(text);
    }

    [GeneratedRegex(@"^\s*(?:(?:\d{1,2}:)?\d{1,2}:\d{2})\s*$")]
    private static partial Regex TimerTextPattern();
}
