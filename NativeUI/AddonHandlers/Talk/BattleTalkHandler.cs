// <copyright file="BattleTalkHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Character
{
  /// <summary>
  ///     Handles translation for the "Character" addon using AtkValues and
  ///     StringArrayData.
  ///     Lifecycle-safe: extracts and applies values within valid memory scope per
  ///     frame.
  /// </summary>
  public class BattleTalkHandler : GenericAddonHandler<BattleTalkMessage>
  {
    /// <summary>
    ///     Initializes a new instance of the <see cref="BattleTalkHandler" />
    ///     class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public BattleTalkHandler(
        Config config,
        TranslationService translationService) : base(
        "_BattleTalk",
        config,
        translationService,
       useAtkValues: false,
       useStringArray: true,
       stringArrayDataType: StringArrayType.BattleTalk)
    {
      this.RegisterHandler(AddonEvent.PreSetup, this.OnPreSetup);
      this.RegisterHandler(AddonEvent.PreRefresh, this.OnPreRefresh);
      this.RegisterHandler(
          AddonEvent.PreRequestedUpdate,
          this.OnPreRequestedUpdate);
    }
  }
}