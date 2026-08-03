// <copyright file="HoverTooltipRichOriginalPresentationResolver.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TextPresentation;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
/// Identifies one native text-node payload that may be copied for rich
/// original swap presentation.
/// </summary>
/// <param name="TextNodeAddress">The live text-node address used only during synchronous capture.</param>
/// <param name="ExpectedOriginalText">The plain original text selected for display.</param>
internal readonly record struct RichOriginalTextCaptureRequest(
    nint TextNodeAddress,
    string ExpectedOriginalText);

/// <summary>
/// Reuses or captures rich original payloads for hover tooltip entries without
/// retrying native reads while their displayed text is unchanged.
/// </summary>
internal static class HoverTooltipRichOriginalPresentationResolver
{
    /// <summary>
    /// Resolves the rich original payload for one tooltip body.
    /// </summary>
    /// <param name="previousBody">The previously displayed tooltip body.</param>
    /// <param name="previousDisplaysOriginalSwapText">Whether the previous entry displayed original swap text.</param>
    /// <param name="previousCaptureResolved">Whether the previous rich capture has already been attempted.</param>
    /// <param name="previousPresentation">The previously captured owned presentation, if any.</param>
    /// <param name="currentBody">The tooltip body selected for the current frame.</param>
    /// <param name="displaysOriginalSwapText">Whether the current body displays original swap text.</param>
    /// <param name="captureRequest">The synchronous native capture request, if available.</param>
    /// <param name="capture">The capture function that owns payload bytes before returning.</param>
    /// <param name="captureResolved">Whether the caller should retain the capture result, including a failure.</param>
    /// <returns>The owned rich original presentation, or <see langword="null" /> for plain fallback.</returns>
    public static RichOriginalTextPresentation? Resolve(
        string? previousBody,
        bool previousDisplaysOriginalSwapText,
        bool previousCaptureResolved,
        RichOriginalTextPresentation? previousPresentation,
        string currentBody,
        bool displaysOriginalSwapText,
        RichOriginalTextCaptureRequest? captureRequest,
        Func<RichOriginalTextCaptureRequest, RichOriginalTextPresentation?>? capture,
        out bool captureResolved)
    {
        captureResolved = false;
        if (!displaysOriginalSwapText ||
            !captureRequest.HasValue ||
            capture == null)
        {
            return null;
        }

        if (previousDisplaysOriginalSwapText &&
            previousCaptureResolved &&
            string.Equals(previousBody, currentBody, StringComparison.Ordinal))
        {
            captureResolved = true;
            return previousPresentation;
        }

        captureResolved = true;
        try
        {
            return capture(captureRequest.Value);
        }
        catch
        {
            return null;
        }
    }
}
