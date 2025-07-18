// <copyright file="CharacterWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Character;

/// <summary>
///     Handles translation for the "Character" addon using AtkValues and
///     StringArrayData.
///     Lifecycle-safe: extracts and applies values within valid memory scope per
///     frame.
/// </summary>
public class CharacterWindowHandler : GenericAddonHandler<GameWindow>
{
  /// <summary>
  ///     Initializes a new instance of the <see cref="CharacterWindowHandler" />
  ///     class.
  /// </summary>
  /// <param name="config">The configuration settings for the plugin.</param>
  /// <param name="translationService">The service used for translating text.</param>
  public CharacterWindowHandler(
      Config config,
      TranslationService translationService) : base(
      "Character",
      config,
      translationService,
      true,
      true,
      StringArrayType.Character)
  {
    this.RegisterHandler(AddonEvent.PreSetup, this.ApplyTranslated);
    this.RegisterHandler(AddonEvent.PreRefresh, this.ExtractAndTranslate);
    this.RegisterHandler(
        AddonEvent.PreRequestedUpdate,
        this.ApplyTranslated);

    this.RegisterHandler(AddonEvent.PostRequestedUpdate, this.ApplyTranslated);
  }
}