using System.Collections.Generic;
using System.Linq;

using Dalamud.Logging;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public HashSet<string> UiElementsLabels = new();

    public void ParseUi()
    {
      ExcelSheet<Addon> uiStuffz = DManager.GetExcelSheet<Addon>(ClientState.ClientLanguage);

      var addonList = uiStuffz?.ToList();

      PluginLog.Warning($"Addon list: {uiStuffz?.RowCount.ToString()}");
      if (uiStuffz != null)
      {
        foreach (var a in uiStuffz)
        {
          this.UiElementsLabels.Add(a.Text.ToString());
          PluginLog.Verbose($"Sheet row: {a.RowId}: {a.Text.ToString()}");
        }
      }
    }
  }
}
