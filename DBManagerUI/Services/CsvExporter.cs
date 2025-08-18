// <copyright file="CsvExporter.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.DBManagerUI.Services
{
  using System;
  using System.Collections.Generic;
  using System.Reflection;
  using System.Text;

  using Microsoft.EntityFrameworkCore.Metadata;

  /// <summary>
  /// Builds CSV text from rows and known properties.
  /// </summary>
  public class CsvExporter
  {
    /// <summary>
    /// Builds CSV string for the given rows and properties.
    /// </summary>
    /// <param name="rows">Row objects.</param>
    /// <param name="props">Properties to include.</param>
    /// <returns>CSV text.</returns>
    public string BuildCsv(IList<object> rows, IReadOnlyList<IProperty> props)
    {
      var sb = new StringBuilder();

      for (int i = 0; i < props.Count; i++)
      {
        if (i > 0)
        {
          sb.Append(',');
        }

        sb.Append(this.CsvEscape(props[i].Name));
      }

      sb.AppendLine();

      foreach (var r in rows)
      {
        for (int i = 0; i < props.Count; i++)
        {
          if (i > 0)
          {
            sb.Append(',');
          }

          var pi = props[i].PropertyInfo!;
          var v = this.SafeGetValue(r, pi);
          var text = this.RenderCellValue(v);
          sb.Append(this.CsvEscape(text));
        }

        sb.AppendLine();
      }

      return sb.ToString();
    }

    private object? SafeGetValue(object obj, PropertyInfo pi)
    {
      try
      {
        return pi.GetValue(obj);
      }
      catch { return null; }
    }

    private string RenderCellValue(object? val)
    {
      if (val == null) { return "(null)"; }
      if (val is byte[] bytes) { return $"[BLOB {bytes.Length} bytes]"; }

      string s = val.ToString() ?? string.Empty;
      if (s.Length > 256) { s = s.Substring(0, 256) + "…"; }
      return s;
    }

    private string CsvEscape(string text)
    {
      if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
      {
        return "\"" + text.Replace("\"", "\"\"") + "\"";
      }

      return text;
    }
  }
}
