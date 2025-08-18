// <copyright file="PropertyEditorRegistry.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.DBManagerUI.Services
{
  /// <summary>Draws editors for property values by type.</summary>
  public sealed class PropertyEditorRegistry
  {
    private readonly List<IPropertyEditor> editors = new()
    {
      new StringEditor(),
      new IntEditor(),
      new LongEditor(),
      new FloatEditor(),
      new DoubleEditor(),
      new BoolEditor(),
      new EnumEditor(),
    };

    /// <summary>Draws an editor for the given property; returns true if value changed.</summary>
    public bool Draw(PropertyInfo pi, ref object? value)
    {
      foreach (var e in this.editors)
      {
        if (e.CanEdit(pi))
        {
          return e.Draw(pi, ref value);
        }
      }

      // Fallback: string round-trip.
      string s = value?.ToString() ?? string.Empty;
      bool changed = ImGui.InputText("##txt", ref s, 2048);
      if (changed) { value = s; }
      return changed;
    }
  }

  /// <summary>Editor contract.</summary>
  public interface IPropertyEditor
  {
    bool CanEdit(PropertyInfo pi);
    bool Draw(PropertyInfo pi, ref object? value);
  }

  internal sealed class StringEditor : IPropertyEditor
  {
    public bool CanEdit(PropertyInfo pi) => pi.PropertyType == typeof(string);
    public bool Draw(PropertyInfo pi, ref object? value)
    {
      string s = value as string ?? string.Empty;
      bool changed = ImGui.InputText("##txt", ref s, 4096);
      if (changed) { value = s; }
      return changed;
    }
  }

  internal sealed class IntEditor : IPropertyEditor
  {
    public bool CanEdit(PropertyInfo pi) => pi.PropertyType == typeof(int) || pi.PropertyType == typeof(int?);
    public bool Draw(PropertyInfo pi, ref object? value)
    {
      int v = value is int iv ? iv : 0;
      bool changed = ImGui.InputInt("##int", ref v);
      if (changed) { value = v; }
      return changed;
    }
  }

  internal sealed class LongEditor : IPropertyEditor
  {
    public bool CanEdit(PropertyInfo pi) => pi.PropertyType == typeof(long) || pi.PropertyType == typeof(long?);
    public bool Draw(PropertyInfo pi, ref object? value)
    {
      string s = (value is long lv ? lv : 0L).ToString(CultureInfo.InvariantCulture);
      bool changed = ImGui.InputText("##long", ref s, 64);
      if (changed && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) { value = parsed; }
      return changed;
    }
  }

  internal sealed class FloatEditor : IPropertyEditor
  {
    public bool CanEdit(PropertyInfo pi) => pi.PropertyType == typeof(float) || pi.PropertyType == typeof(float?);
    public bool Draw(PropertyInfo pi, ref object? value)
    {
      float v = value is float fv ? fv : 0f;
      bool changed = ImGui.InputFloat("##float", ref v);
      if (changed) { value = v; }
      return changed;
    }
  }

  internal sealed class DoubleEditor : IPropertyEditor
  {
    public bool CanEdit(PropertyInfo pi) => pi.PropertyType == typeof(double) || pi.PropertyType == typeof(double?);
    public bool Draw(PropertyInfo pi, ref object? value)
    {
      string s = (value is double dv ? dv : 0d).ToString(CultureInfo.InvariantCulture);
      bool changed = ImGui.InputText("##double", ref s, 128);
      if (changed && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) { value = parsed; }
      return changed;
    }
  }

  internal sealed class BoolEditor : IPropertyEditor
  {
    public bool CanEdit(PropertyInfo pi) => pi.PropertyType == typeof(bool) || pi.PropertyType == typeof(bool?);
    public bool Draw(PropertyInfo pi, ref object? value)
    {
      bool v = value is bool b && b;
      bool changed = ImGui.Checkbox("##bool", ref v);
      if (changed) { value = v; }
      return changed;
    }
  }

  internal sealed class EnumEditor : IPropertyEditor
  {
    public bool CanEdit(PropertyInfo pi) => (Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType).IsEnum;
    public bool Draw(PropertyInfo pi, ref object? value)
    {
      // minimal: free-text; later you can upgrade to a combo of Enum.GetNames(...)
      string s = value?.ToString() ?? string.Empty;
      bool changed = ImGui.InputText("##enum", ref s, 256);
      if (changed)
      {
        var t = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
        try { value = Enum.Parse(t, s, ignoreCase: true); } catch { /* keep old */ }
      }
      return changed;
    }
  }
}
