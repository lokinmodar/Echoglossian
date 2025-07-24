// <copyright file="CharacterWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Character
{
  /// <summary>
  ///     Handles translation for the "MainCommand" addon using AtkValues and
  ///     StringArrayData.
  ///     Lifecycle-safe: extracts and applies values within valid memory scope per
  ///     frame.
  /// </summary>
  public class MainCommandHandler : GenericAddonHandler<GameWindow>
  {
    /// <summary>
    ///     Initializes a new instance of the <see cref="MainCommandHandler" />
    ///     class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public MainCommandHandler(
        Config config,
        TranslationService translationService)
      : base(
        "_MainCommand",
        config,
        translationService,
        true,
        false)
    {
      this.RegisterHandler(AddonEvent.PreSetup, this.OnPreSetup);
      this.RegisterHandler(AddonEvent.PreRefresh, this.OnPreRefresh);
      this.RegisterHandler(
          AddonEvent.PreRequestedUpdate,
          this.OnPreRequestedUpdate);
    }
  }
}