// <copyright file="ToDoPayload.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Security.Cryptography;
using System.Text;

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Represents one captured ToDo text node.
/// </summary>
/// <param name="NodeKey">The stable structural text-node identity.</param>
/// <param name="NodeId">The native node id.</param>
/// <param name="Text">The visible node text.</param>
/// <param name="IsTimerNode">Whether the node displays volatile timer text.</param>
internal sealed record ToDoCapturedText(
    string NodeKey,
    int NodeId,
    string Text,
    bool IsTimerNode)
{
    /// <summary>
    ///     Initializes a captured text row for callers that only have a unique
    ///     native node id.
    /// </summary>
    /// <param name="nodeId">The native node id.</param>
    /// <param name="text">The visible node text.</param>
    /// <param name="isTimerNode">Whether the node displays volatile timer text.</param>
    public ToDoCapturedText(int nodeId, string text, bool isTimerNode)
        : this($"{nodeId}:0", nodeId, text, isTimerNode)
    {
    }
}

/// <summary>
///     Represents the visible text captured from the dedicated ToDo surface.
/// </summary>
internal sealed class ToDoPayload
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ToDoPayload" /> class.
    /// </summary>
    /// <param name="visibleTexts">The captured visible texts in display order.</param>
    public ToDoPayload(IReadOnlyList<ToDoCapturedText> visibleTexts)
    {
        this.VisibleTexts = [.. visibleTexts];
    }

    /// <summary>
    ///     Gets the captured visible texts in display order.
    /// </summary>
    public IReadOnlyList<ToDoCapturedText> VisibleTexts { get; }

    /// <summary>
    ///     Gets the visible texts that are stable and translatable.
    /// </summary>
    /// <returns>The non-timer text nodes in display order.</returns>
    public IReadOnlyList<ToDoCapturedText> GetTranslatableTexts()
    {
        return this.VisibleTexts.Where(text => !text.IsTimerNode).ToArray();
    }

    /// <summary>
    ///     Computes a stable source hash that excludes volatile timer nodes.
    /// </summary>
    /// <returns>The uppercase SHA-256 hash.</returns>
    public string ComputeSourceContentHash()
    {
        var builder = new StringBuilder();
        foreach (var text in this.GetTranslatableTexts())
        {
            builder.Append(text.NodeKey)
                .Append('|')
                .Append(text.Text)
                .Append('\u001F');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
