// <copyright file="CharacterWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Handlers;

namespace Echoglossian.NativeUI.AddonHandlers;

/// <summary>
/// Handles the translation and lifecycle events for the Character Window addon.
/// </summary>
public unsafe class CharacterWindowHandler : GenericAddonHandler
{
  /// <summary>
  /// Initializes a new instance of the <see cref="CharacterWindowHandler"/> class.
  /// </summary>
  /// <param name="config">The configuration settings for the plugin.</param>
  /// <param name="translationService">The service used for translating text.</param>
  public CharacterWindowHandler(Config config, TranslationService translationService)
    : base("Character", config, translationService, useAtkValues: true, useStringArray: true, stringArrayDataType: StringArrayType.Character)
  {
    this.RegisterHandler(AddonEvent.PreSetup, this.ExtractAndTranslate);
    this.RegisterHandler(AddonEvent.PreRefresh, this.ApplyTranslated);
    this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.ApplyTranslated);
  }
}
