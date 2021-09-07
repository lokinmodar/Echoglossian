// <copyright file="Utils.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private void SaveConfig()
    {
      this.configuration.Lang = languageInt;

      this.configuration.ChosenLanguages = this.chosenLanguages;

      this.pluginInterface.SavePluginConfig(this.configuration);
    }
  }
}