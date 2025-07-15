// <copyright file="Hud2WindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Handlers;

namespace Echoglossian.NativeUI.AddonHandlers.Hud;

/// <summary>
///     Handles translation for the "Hud" addon using AtkValues and
///     StringArrayData.
///     Lifecycle-safe: extracts and applies values within valid memory scope per
///     frame.
/// </summary>
public class Hud2WindowHandler : GenericAddonHandler
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Hud2WindowHandler" /> class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public Hud2WindowHandler(
        Config config,
        TranslationService translationService) : base(
        "Hud2",
        config,
        translationService,
        true,
        true,
        StringArrayType.Hud2)
    {
        this.RegisterHandler(AddonEvent.PreSetup, this.ExtractAndTranslate);
        this.RegisterHandler(AddonEvent.PreRefresh, this.ApplyTranslated);
        this.RegisterHandler(
            AddonEvent.PreRequestedUpdate,
            this.ApplyTranslated);

        // this.RegisterHandler(AddonEvent.PostRequestedUpdate, this.ApplyTranslated);
    }
}