// <copyright file="TooltipAddonRichOriginalTextPresentationFactory.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Lumina.Text.ReadOnly;
using LuminaSeStringBuilder = Lumina.Text.SeStringBuilder;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
/// Builds one combined rich original-text presentation for Tooltip addon swap
/// overlays from the ordered native text-node payload segments.
/// </summary>
internal static class TooltipAddonRichOriginalTextPresentationFactory
{
    /// <summary>
    /// Creates one combined rich original-text presentation from the ordered
    /// Tooltip text-node payload segments.
    /// </summary>
    /// <param name="plainText">The readable plain-text fallback.</param>
    /// <param name="payloadSegments">
    /// The ordered payload segments, one per visible Tooltip text node.
    /// </param>
    /// <returns>
    /// The combined presentation when every segment is available; otherwise,
    /// <see langword="null" />.
    /// </returns>
    public static RichOriginalTextPresentation? Create(
        string plainText,
        IReadOnlyList<byte[]?> payloadSegments)
    {
        if (string.IsNullOrWhiteSpace(plainText) ||
            payloadSegments.Count == 0)
        {
            return null;
        }

        var builder = new LuminaSeStringBuilder();
        for (var index = 0; index < payloadSegments.Count; index++)
        {
            var payloadSegment = payloadSegments[index];
            if (payloadSegment == null || payloadSegment.Length == 0)
            {
                return null;
            }

            if (index > 0)
            {
                builder.Append('\n');
            }

            builder.Append(new ReadOnlySeString(payloadSegment));
        }

        var combinedPayload = builder.ToReadOnlySeString().Data.ToArray();
        return combinedPayload.Length == 0
            ? null
            : new RichOriginalTextPresentation(
                plainText,
                combinedPayload);
    }
}
