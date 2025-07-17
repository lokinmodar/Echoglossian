// <copyright file="HudWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Hud;

/// <summary>
///     Handles translation for the "Hud" addon using AtkValues and
///     StringArrayData.
///     Lifecycle-safe: extracts and applies values within valid memory scope per
///     frame.
/// </summary>
public class HudWindowHandler : GenericAddonHandler<GameWindow>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="HudWindowHandler" /> class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public HudWindowHandler(
        Config config,
        TranslationService translationService) : base(
        "Hud",
        config,
        translationService,
        true,
        true,
        StringArrayType.Hud)
    {
        this.RegisterHandler(AddonEvent.PreSetup, this.ExtractAndTranslate);
        this.RegisterHandler(AddonEvent.PreRefresh, this.ApplyTranslated);
        this.RegisterHandler(
            AddonEvent.PreRequestedUpdate,
            this.ApplyTranslated);

        // this.RegisterHandler(AddonEvent.PostRequestedUpdate, this.ApplyTranslated);
    }
}