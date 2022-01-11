using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Data;
using Dalamud.Logging;
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel;
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

      ExcelSheet<Addon> uiStuffz = DManager.GetExcelSheet<Addon>(ClientState.ClientLanguage);

      var addonList = uiStuffz?.ToList();

      PluginLog.LogWarning($"Addon list: {uiStuffz?.RowCount.ToString()}");
      if (uiStuffz != null)
      {
        foreach (var a in uiStuffz)
        {
          this.UiElementsLabels.Add(a.Text.ToString());
          //PluginLog.LogError($"Sheet row: {a.RowId}: {a.Text.ToString()}");
        }
      }
    }
  }
}

