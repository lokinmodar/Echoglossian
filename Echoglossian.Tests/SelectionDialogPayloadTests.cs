// <copyright file="SelectionDialogPayloadTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.SelectionDialogs;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers overlay layout policies for generic selection-dialog payloads.
/// </summary>
public sealed class SelectionDialogPayloadTests
{
    /// <summary>
    ///     Ensures option-only dialog payloads can render entirely in the
    ///     overlay body without promoting the first option into a title slot.
    /// </summary>
    [Fact]
    public void ToOverlayParts_DoesNotPromoteFirstTextToTitle_WhenBodyOnlyRequested()
    {
        var payload = SelectionDialogPayload.FromTextNodes(
            [1, 2, 3],
            ["Purchase a Mini Cactpot ticket", "Information on the Mini Cactpot", "Nothing"]);

        var parts = payload.ToOverlayParts(treatFirstTextAsTitle: false);

        Assert.Equal(string.Empty, parts.Title);
        Assert.Equal(
            "Purchase a Mini Cactpot ticket\nInformation on the Mini Cactpot\nNothing",
            parts.Body);
    }
}
