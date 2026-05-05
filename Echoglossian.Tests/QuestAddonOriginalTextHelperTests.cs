// <copyright file="QuestAddonOriginalTextHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Quest;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the reuse guard that decides when quest addon rows should keep
///     their previously captured original text.
/// </summary>
public class QuestAddonOriginalTextHelperTests
{
    /// <summary>
    ///     Ensures a row that still shows the previous original text keeps the
    ///     same source identity.
    /// </summary>
    [Fact]
    public void ResolveOriginalVisibleText_VisibleOriginal_ReusesPreviousOriginal()
    {
        var resolved = QuestAddonOriginalTextHelper.ResolveOriginalVisibleText(
            "Defeat squirrels 0/3",
            "Defeat squirrels 0/3",
            "Derrote esquilos 0/3");

        Assert.Equal("Defeat squirrels 0/3", resolved);
    }

    /// <summary>
    ///     Ensures a row that still shows the previously applied translated
    ///     text resolves back to the original source text.
    /// </summary>
    [Fact]
    public void ResolveOriginalVisibleText_VisibleTranslated_ReusesPreviousOriginal()
    {
        var resolved = QuestAddonOriginalTextHelper.ResolveOriginalVisibleText(
            "Derrote esquilos 0/3",
            "Defeat squirrels 0/3",
            "Derrote esquilos 0/3");

        Assert.Equal("Defeat squirrels 0/3", resolved);
    }

    /// <summary>
    ///     Ensures a row whose visible text changed to a different quest or
    ///     objective does not keep the previous original text by slot only.
    /// </summary>
    [Fact]
    public void ResolveOriginalVisibleText_NewVisibleText_UsesNewVisibleText()
    {
        var resolved = QuestAddonOriginalTextHelper.ResolveOriginalVisibleText(
            "Speak with Alphinaud",
            "Defeat squirrels 0/3",
            "Derrote esquilos 0/3");

        Assert.Equal("Speak with Alphinaud", resolved);
    }
}
