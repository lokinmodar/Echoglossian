using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;

//using FrameworkI = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public HashSet<string> UiElementsLabels = new();

    public void ParseUi()
    {
      /*      unsafe
            {
              var ptr =
                (IntPtr) FrameworkI.Instance()->UIModule->GetExcelModule()->
                  GetSheetByName("Addon");
            }*/

      var uiStuffz = DManager.GetExcelSheet<Addon>(ClientState.ClientLanguage);

      var addonList = uiStuffz?.ToList();

      PluginLog.LogVerbose($"Addon list: {uiStuffz?.RowCount.ToString()}");
      if (uiStuffz != null)
      {
        foreach (var a in uiStuffz)
        {
          this.UiElementsLabels.Add(a.Text.ToString());
        }
      }

      //PluginLog.LogError($"Sheet row: {a.RowId}: {a.Text.ToString()}");
    }
  }
}