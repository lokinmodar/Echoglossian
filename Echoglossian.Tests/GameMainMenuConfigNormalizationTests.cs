// <copyright file="GameMainMenuConfigNormalizationTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers unified config behavior for the shared game-main-menu scope.
/// </summary>
public class GameMainMenuConfigNormalizationTests
{
    /// <summary>
    ///     Ensures the shared game-main-menu scope normalizes the
    ///     AddonContextMenuTitle toggle and display mode when only MainCommand
    ///     was previously enabled.
    /// </summary>
    [Fact]
    public void NormalizeGameMainMenuTranslationSettings_MainCommandOnly_SynchronizesAddonContext()
    {
        var config = new Config
        {
            TranslateMainCommandWindow = true,
            MainCommandWindowTranslationDisplayMode =
                JournalTranslationDisplayMode.TooltipTranslation,
            TranslateAddonContextMenuTitle = false,
            AddonContextMenuTitleTranslationDisplayMode =
                JournalTranslationDisplayMode.NativeUiTranslation,
            TranslateActionMenuWindow = false,
            ActionMenuWindowTranslationDisplayMode =
                JournalTranslationDisplayMode.NativeUiTranslation,
        };

        var changed = config.NormalizeGameMainMenuTranslationSettings();

        Assert.True(changed);
        Assert.True(config.TranslateGameMainMenu);
        Assert.True(config.TranslateMainCommandWindow);
        Assert.True(config.TranslateAddonContextMenuTitle);
        Assert.Equal(
            JournalTranslationDisplayMode.TooltipTranslation,
            config.GameMainMenuWindowTranslationDisplayMode);
        Assert.Equal(
            JournalTranslationDisplayMode.TooltipTranslation,
            config.MainCommandWindowTranslationDisplayMode);
        Assert.Equal(
            JournalTranslationDisplayMode.TooltipTranslation,
            config.AddonContextMenuTitleTranslationDisplayMode);
        Assert.False(config.TranslateActionMenuWindow);
        Assert.Equal(
            JournalTranslationDisplayMode.NativeUiTranslation,
            config.ActionMenuWindowTranslationDisplayMode);
    }

    /// <summary>
    ///     Ensures the shared game-main-menu scope normalizes MainCommand when
    ///     only AddonContextMenuTitle was previously enabled.
    /// </summary>
    [Fact]
    public void NormalizeGameMainMenuTranslationSettings_AddonContextOnly_SynchronizesMainCommand()
    {
        var config = new Config
        {
            TranslateMainCommandWindow = false,
            MainCommandWindowTranslationDisplayMode =
                JournalTranslationDisplayMode.NativeUiTranslation,
            TranslateAddonContextMenuTitle = true,
            AddonContextMenuTitleTranslationDisplayMode =
                JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
            TranslateActionMenuWindow = true,
            ActionMenuWindowTranslationDisplayMode =
                JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
        };

        var changed = config.NormalizeGameMainMenuTranslationSettings();

        Assert.True(changed);
        Assert.True(config.TranslateGameMainMenu);
        Assert.True(config.TranslateMainCommandWindow);
        Assert.True(config.TranslateAddonContextMenuTitle);
        Assert.Equal(
            JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
            config.GameMainMenuWindowTranslationDisplayMode);
        Assert.Equal(
            JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
            config.MainCommandWindowTranslationDisplayMode);
        Assert.Equal(
            JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
            config.AddonContextMenuTitleTranslationDisplayMode);
        Assert.True(config.TranslateActionMenuWindow);
        Assert.Equal(
            JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
            config.ActionMenuWindowTranslationDisplayMode);
    }

    /// <summary>
    ///     Ensures explicit updates to the shared game-main-menu settings touch
    ///     only MainCommand and AddonContextMenuTitle, not ActionMenu.
    /// </summary>
    [Fact]
    public void SetGameMainMenuTranslationSettings_DoesNotModifyActionMenuSettings()
    {
        var config = new Config
        {
            TranslateMainCommandWindow = false,
            TranslateAddonContextMenuTitle = false,
            TranslateActionMenuWindow = true,
            ActionMenuWindowTranslationDisplayMode =
                JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
        };

        var changed = config.SetGameMainMenuTranslationSettings(
            true,
            JournalTranslationDisplayMode.TooltipTranslation);

        Assert.True(changed);
        Assert.True(config.TranslateMainCommandWindow);
        Assert.True(config.TranslateAddonContextMenuTitle);
        Assert.Equal(
            JournalTranslationDisplayMode.TooltipTranslation,
            config.MainCommandWindowTranslationDisplayMode);
        Assert.Equal(
            JournalTranslationDisplayMode.TooltipTranslation,
            config.AddonContextMenuTitleTranslationDisplayMode);
        Assert.True(config.TranslateActionMenuWindow);
        Assert.Equal(
            JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
            config.ActionMenuWindowTranslationDisplayMode);
    }

    /// <summary>
    ///     Ensures ActionMenu remains independent and does not enable the
    ///     shared game-main-menu scope by itself.
    /// </summary>
    [Fact]
    public void NormalizeGameMainMenuTranslationSettings_ActionMenuOnly_RemainsIndependent()
    {
        var config = new Config
        {
            TranslateMainCommandWindow = false,
            TranslateAddonContextMenuTitle = false,
            TranslateActionMenuWindow = true,
            ActionMenuWindowTranslationDisplayMode =
                JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
        };

        var changed = config.NormalizeGameMainMenuTranslationSettings();

        Assert.False(changed);
        Assert.False(config.TranslateGameMainMenu);
        Assert.False(config.TranslateMainCommandWindow);
        Assert.False(config.TranslateAddonContextMenuTitle);
        Assert.True(config.TranslateActionMenuWindow);
        Assert.Equal(
            JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
            config.ActionMenuWindowTranslationDisplayMode);
    }
}
