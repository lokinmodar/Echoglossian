// <copyright file="Utils.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private void SaveConfig()
    {
      this.configuration.Lang = languageInt;

      this.pluginInterface.SavePluginConfig(this.configuration);
    }
  }
}