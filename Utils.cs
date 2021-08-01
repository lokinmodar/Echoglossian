namespace Echoglossian
{
    public partial class Echoglossian
    {
        private void SaveConfig()
        {
            _configuration.Lang = _languageInt;

            _configuration.ChosenLanguages = _chosenLanguages;

            _pluginInterface.SavePluginConfig(_configuration);
        }
    }
}