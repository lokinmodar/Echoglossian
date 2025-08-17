// <copyright file="EditModal.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the CC BY-NC-ND 4.0 International Public License.
// </copyright>

namespace Echoglossian.DBManagerUI.Components
{
  /// <summary>
  /// Modal dialog for editing a single entity instance.
  /// </summary>
  public class EditModal
  {
    private readonly Func<IReadOnlyList<IProperty>?> getScalarProps;
    private readonly Func<HashSet<string>?> getPkNames;
    private readonly Action<object> onSave;
    private readonly Action<object> onDelete;

    private bool isOpen = false;
    private object? entity;
    private Type? entityType;
    private readonly Dictionary<string, object?> edited = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EditModal"/> class.
    /// </summary>
    public EditModal(
      Func<IReadOnlyList<IProperty>?> getScalarProps,
      Func<HashSet<string>?> getPkNames,
      Action<object> onSave,
      Action<object> onDelete)
    {
      this.getScalarProps = getScalarProps;
      this.getPkNames = getPkNames;
      this.onSave = onSave;
      this.onDelete = onDelete;
    }

    /// <summary>
    /// Opens the modal for an entity.
    /// </summary>
    /// <param name="entity">Entity instance.</param>
    public void Open(object entity)
    {
      this.entity = entity;
      this.entityType = entity.GetType();
      this.edited.Clear();

      var props = this.getScalarProps();
      if (props != null)
      {
        foreach (var p in props)
        {
          object? v = this.SafeGetValue(entity, p.PropertyInfo!);
          this.edited[p.Name] = v;
        }
      }

      this.isOpen = true;
      ImGui.OpenPopup("Edit Record");
    }

    /// <summary>
    /// Closes the modal.
    /// </summary>
    public void Close()
    {
      this.isOpen = false;
      this.entity = null;
      this.entityType = null;
      this.edited.Clear();
    }

    /// <summary>
    /// Draw the modal if open.
    /// </summary>
    public void Draw()
    {
      if (!this.isOpen)
      {
        return;
      }

      bool open = true;
      if (ImGui.BeginPopupModal("Edit Record", ref open, ImGuiWindowFlags.AlwaysAutoResize))
      {
        var props = this.getScalarProps();
        var pkNames = this.getPkNames();

        if (this.entity == null || this.entityType == null || props == null)
        {
          ImGui.TextColored(new Vector4(1, 0.4f, 0.4f, 1), "Unable to load entity for editing.");
        }
        else
        {
          foreach (var prop in props)
          {
            string name = prop.Name;
            var pi = prop.PropertyInfo!;
            bool isPk = pkNames != null && pkNames.Contains(name);
            bool isDate = pi.PropertyType == typeof(DateTime) || pi.PropertyType == typeof(DateTime?)
                          || pi.PropertyType == typeof(DateTimeOffset) || pi.PropertyType == typeof(DateTimeOffset?);

            bool editable = !isPk && !isDate && pi.CanWrite;

            ImGui.PushID(name);
            ImGui.TextUnformatted(name);
            ImGui.SameLine(220);

            object? current = this.edited.TryGetValue(name, out var v) ? v : null;

            if (!editable)
            {
              ImGui.TextDisabled(this.RenderCellValue(current));
            }
            else
            {
              this.DrawEditorForValue(pi.PropertyType, name, current);
            }

            ImGui.PopID();
          }
        }

        ImGui.Separator();

        if (ImGui.Button("Save"))
        {
          if (this.entity != null)
          {
            this.ApplyEdits(this.entity);
            this.onSave(this.entity);
          }
        }

        ImGui.SameLine();

        if (ImGui.Button("Delete"))
        {
          if (this.entity != null)
          {
            this.onDelete(this.entity);
          }
        }

        ImGui.SameLine();

        if (ImGui.Button("Cancel"))
        {
          ImGui.CloseCurrentPopup();
          this.Close();
        }

        ImGui.EndPopup();
      }

      if (!open)
      {
        this.Close();
      }
    }

    private void ApplyEdits(object target)
    {
      var props = this.getScalarProps();
      if (props == null)
      {
        return;
      }

      foreach (var prop in props)
      {
        var pi = prop.PropertyInfo!;
        if (!pi.CanWrite)
        {
          continue;
        }

        bool isPk = this.getPkNames()?.Contains(prop.Name) ?? false;
        bool isDate = pi.PropertyType == typeof(DateTime) || pi.PropertyType == typeof(DateTime?)
                      || pi.PropertyType == typeof(DateTimeOffset) || pi.PropertyType == typeof(DateTimeOffset?);

        if (isPk || isDate)
        {
          continue;
        }

        if (this.edited.TryGetValue(prop.Name, out var newVal))
        {
          try
          {
            if (newVal != null && !pi.PropertyType.IsAssignableFrom(newVal.GetType()))
            {
              newVal = this.ChangeTypeFromObject(newVal, pi.PropertyType);
            }

            pi.SetValue(target, newVal);
          }
          catch
          {
            // Ignore conversion/set errors; keep existing value.
          }
        }
      }
    }

    private void DrawEditorForValue(Type type, string propName, object? current)
    {
      if (type == typeof(string))
      {
        string s = current as string ?? string.Empty;
        if (ImGui.InputText("##txt", ref s, 4096))
        {
          this.edited[propName] = s;
        }

        return;
      }

      if (type == typeof(int) || type == typeof(int?))
      {
        int v = current is int iv ? iv : 0;
        if (ImGui.InputInt("##int", ref v))
        {
          this.edited[propName] = v;
        }

        return;
      }

      if (type == typeof(long) || type == typeof(long?))
      {
        long v = current is long lv ? lv : 0L;
        string s = v.ToString(CultureInfo.InvariantCulture);
        if (ImGui.InputText("##long", ref s, 64))
        {
          if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
          {
            this.edited[propName] = parsed;
          }
        }

        return;
      }

      if (type == typeof(float) || type == typeof(float?))
      {
        float v = current is float fv ? fv : 0f;
        if (ImGui.InputFloat("##float", ref v))
        {
          this.edited[propName] = v;
        }

        return;
      }

      if (type == typeof(double) || type == typeof(double?))
      {
        double v = current is double dv ? dv : 0d;
        string s = v.ToString(CultureInfo.InvariantCulture);
        if (ImGui.InputText("##double", ref s, 128))
        {
          if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
          {
            this.edited[propName] = parsed;
          }
        }

        return;
      }

      if (type == typeof(bool) || type == typeof(bool?))
      {
        bool v = current is bool bv && bv;
        if (ImGui.Checkbox("##bool", ref v))
        {
          this.edited[propName] = v;
        }

        return;
      }

      if (type.IsEnum)
      {
        string s = current?.ToString() ?? string.Empty;
        if (ImGui.InputText("##enum", ref s, 256))
        {
          try
          {
            object parsed = Enum.Parse(type, s, ignoreCase: true);
            this.edited[propName] = parsed;
          }
          catch
          {
          }
        }

        return;
      }

      string f = current?.ToString() ?? string.Empty;
      if (ImGui.InputText("##txt", ref f, 2048))
      {
        this.edited[propName] = f;
      }
    }

    private object? SafeGetValue(object obj, PropertyInfo pi)
    {
      try
      {
        return pi.GetValue(obj);
      }
      catch
      {
        return null;
      }
    }

    private object? ChangeTypeFromObject(object value, Type targetType)
    {
      var nonNull = Nullable.GetUnderlyingType(targetType) ?? targetType;

      if (value is string s)
      {
        if (string.IsNullOrEmpty(s) && Nullable.GetUnderlyingType(targetType) != null)
        {
          return null;
        }

        if (nonNull == typeof(Guid))
        {
          return Guid.Parse(s);
        }

        if (nonNull.IsEnum)
        {
          return Enum.Parse(nonNull, s, ignoreCase: true);
        }

        return Convert.ChangeType(s, nonNull, CultureInfo.InvariantCulture);
      }

      if (nonNull.IsInstanceOfType(value))
      {
        return value;
      }

      return Convert.ChangeType(value, nonNull, CultureInfo.InvariantCulture);
    }

    private string RenderCellValue(object? val)
    {
      if (val == null)
      {
        return "(null)";
      }

      if (val is byte[] bytes)
      {
        return $"[BLOB {bytes.Length} bytes]";
      }

      string s = val.ToString() ?? string.Empty;
      if (s.Length > 256)
      {
        s = s.Substring(0, 256) + "…";
      }

      return s;
    }
  }
}
