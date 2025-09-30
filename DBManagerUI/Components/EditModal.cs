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
    private bool pendingOpen = false; // defer OpenPopup to Draw()
    private object? entity;
    private Type? entityType;
    private readonly Dictionary<string, object?> edited = new();

    // Stable popup label: visible title before ###, unique ID after.
    private const string PopupLabel = "Edit Record###EglodbEditModal";

    /// <summary>
    /// Initializes a new instance of the <see cref="EditModal"/> class.
    /// </summary>
    /// <param name="getScalarProps">Accessor for scalar properties.</param>
    /// <param name="getPkNames">Accessor for PK names.</param>
    /// <param name="onSave">Callback on save.</param>
    /// <param name="onDelete">Callback on delete.</param>
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
      this.pendingOpen = true; // do ImGui.OpenPopup in Draw()
    }

    /// <summary>
    /// Closes the modal and clears state.
    /// </summary>
    public void Close()
    {
      this.isOpen = false;
      this.entity = null;
      this.entityType = null;
      this.edited.Clear();
      this.pendingOpen = false;
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

      if (this.pendingOpen)
      {
        this.pendingOpen = false;
        ImGui.OpenPopup(PopupLabel);
      }

      // sensible size + allow resizing
      var center = ImGui.GetMainViewport().GetCenter();
      ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
      ImGui.SetNextWindowSize(new Vector2(720, 520), ImGuiCond.Appearing);
      ImGui.SetNextWindowSizeConstraints(new Vector2(420, 320), new Vector2(6000, 4000));

      bool open = true;
      if (ImGui.BeginPopupModal(PopupLabel, ref open, ImGuiWindowFlags.None))
      {
        var props = this.getScalarProps();
        var pkNames = this.getPkNames();

        if (this.entity == null || this.entityType == null || props == null)
        {
          ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), Resources.UnableToLoadEntityForEditing);
        }
        else
        {
          foreach (var prop in props)
          {
            string name = prop.Name;
            var pi = prop.PropertyInfo!;
            bool isPk = pkNames != null && pkNames.Contains(name);
            bool isDate =
              pi.PropertyType == typeof(DateTime) || pi.PropertyType == typeof(DateTime?) ||
              pi.PropertyType == typeof(DateTimeOffset) || pi.PropertyType == typeof(DateTimeOffset?);

            // "Original*" fields are read-only text
            bool isOriginal = name.StartsWith("Original", StringComparison.OrdinalIgnoreCase);

            bool editable = !isPk && !isDate && !isOriginal && pi.CanWrite;

            ImGui.PushID(name);
            ImGui.TextUnformatted(name);
            ImGui.SameLine(240);

            object? current = this.edited.TryGetValue(name, out var v) ? v : null;

            if (!editable)
            {
              // read-only, wrapped text (Original*, PKs, dates, or non-writable)
              if (current is string sro)
              {
                ImGui.PushTextWrapPos();
                ImGui.TextWrapped(sro);
                ImGui.PopTextWrapPos();
              }
              else
              {
                ImGui.TextDisabled(this.RenderCellValue(current));
              }
            }
            else
            {
              // editable
              this.DrawEditorForValue(pi.PropertyType, name, current);
            }

            ImGui.PopID();
          }
        }

        ImGui.Separator();

        if (ImGui.Button(Resources.Save))
        {
          if (this.entity != null)
          {
            this.ApplyEdits(this.entity);
            this.onSave(this.entity);
          }
        }

        ImGui.SameLine();

        if (ImGui.Button(Resources.Delete))
        {
          if (this.entity != null)
          {
            this.onDelete(this.entity);
          }
        }

        ImGui.SameLine();

        if (ImGui.Button(Resources.Cancel))
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

    /// <summary>
    /// Applies edited values (skips PK/date/Original* fields).
    /// </summary>
    /// <param name="target">Entity instance to update.</param>
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
        bool isDate =
          pi.PropertyType == typeof(DateTime) || pi.PropertyType == typeof(DateTime?) ||
          pi.PropertyType == typeof(DateTimeOffset) || pi.PropertyType == typeof(DateTimeOffset?);
        bool isOriginal = prop.Name.StartsWith("Original", StringComparison.OrdinalIgnoreCase);

        if (isPk || isDate || isOriginal)
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
            // ignore conversion/set errors; keep existing value
          }
        }
      }
    }

    /// <summary>
    /// Draws editors for supported types. Strings are multiline with wrapping.
    /// </summary>
    private void DrawEditorForValue(Type type, string propName, object? current)
    {
      if (type == typeof(string))
      {
        string s = current as string ?? string.Empty;

        // wrapped multiline editor (auto height within min/max lines)
        if (TextInputHelpers.DrawMultilineTextInput("##txt", ref s, minLines: 6, maxLines: 24, flags: ImGuiInputTextFlags.None))
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
        bool v = current is bool b && b;
        if (ImGui.Checkbox("##bool", ref v))
        {
          this.edited[propName] = v;
        }

        return;
      }

      if ((Nullable.GetUnderlyingType(type) ?? type).IsEnum)
      {
        string s = current?.ToString() ?? string.Empty;
        if (ImGui.InputText("##enum", ref s, 256))
        {
          try
          {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            object parsed = Enum.Parse(t, s, ignoreCase: true);
            this.edited[propName] = parsed;
          }
          catch
          {
            // ignore parse failure
          }
        }

        return;
      }

      // fallback: simple text round-trip (multiline)
      {
        string f = current?.ToString() ?? string.Empty;
        if (TextInputHelpers.DrawMultilineTextInput("##txt", ref f, minLines: 4, maxLines: 16))
        {
          this.edited[propName] = f;
        }
      }
    }

    private object? SafeGetValue(object obj, PropertyInfo pi)
    {
      try { return pi.GetValue(obj); } catch { return null; }
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
