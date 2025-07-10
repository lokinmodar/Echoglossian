// <copyright file="CharacterReputeSubWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Handlers;

namespace Echoglossian.NativeUI.AddonHandlers.Character;

/// <summary>
/// Handles translation for the "CharacterClass" addon using AtkValues and StringArrayData.
/// Lifecycle-safe: extracts and applies values within valid memory scope per frame.
/// </summary>
public unsafe class CharacterReputeSubWindowHandler : GenericAddonHandler
{
  /// <summary>
  /// Initializes a new instance of the <see cref="CharacterReputeSubWindowHandler"/> class.
  /// </summary>
  /// <param name="config">The configuration settings for the plugin.</param>
  /// <param name="translationService">The service used for translating text.</param>
  public CharacterReputeSubWindowHandler(Config config, TranslationService translationService)
    : base("CharacterRepute", config, translationService, useAtkValues: true, useStringArray: true, stringArrayDataType: StringArrayType.Character)
  {
    this.RegisterHandler(AddonEvent.PreSetup, this.ApplyTranslated /*this.ExtractAndTranslate*/);
    this.RegisterHandler(AddonEvent.PreRefresh, /*this.ApplyTranslated*/this.ExtractAndTranslate);
    this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.ApplyTranslated);
    this.RegisterHandler(AddonEvent.PostRequestedUpdate, this.ApplyTranslated);
  }
}