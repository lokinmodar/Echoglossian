// <copyright file="MiniTalkWrapWidthHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
/// Resolves MiniTalk-specific wrap-width adjustments without coupling tests to
/// live native nodes.
/// </summary>
internal static class MiniTalkWrapWidthHelper
{
    /// <summary>
    /// Determines whether MiniTalk should add any extra wrap width beyond the
    /// shared container-derived preference.
    /// </summary>
    /// <param name="currentTextWidth">The current width assigned to the text node.</param>
    /// <param name="preferredWrapWidth">
    /// The width already resolved by the shared reflow helper.
    /// </param>
    /// <param name="leftPadding">The current left padding around the text node.</param>
    /// <param name="rightPadding">The current right padding around the text node.</param>
    /// <returns>The additional width that should be added to the wrap width.</returns>
    public static ushort ResolveAdditionalWrapWidth(
        ushort currentTextWidth,
        ushort preferredWrapWidth,
        int leftPadding,
        int rightPadding)
    {
        if (preferredWrapWidth > currentTextWidth)
        {
            return 0;
        }

        return (ushort)Math.Min(
            ushort.MaxValue,
            Math.Max(
                Math.Max(0, leftPadding),
                Math.Max(0, rightPadding)));
    }
}
