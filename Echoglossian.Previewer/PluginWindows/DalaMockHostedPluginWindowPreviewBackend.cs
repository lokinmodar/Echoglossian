// <copyright file="DalaMockHostedPluginWindowPreviewBackend.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;
using Echoglossian.Mock.Hosting;
using Echoglossian.Previewer.UI;
using Echoglossian.Properties;
using System.Drawing;
using System.Reflection;

namespace Echoglossian.Previewer.PluginWindows;

/// <summary>Draws the DalaMock-hosted production plugin windows in the preview host.</summary>
internal sealed class DalaMockHostedPluginWindowPreviewBackend : IPluginWindowPreviewBackend
{
  private const int RequiredCaptureStableFrames = 3;
  private const int MaximumCaptureObservationFrames = 180;
  private readonly HostedPreviewPluginSession session;
  private readonly HostedPluginWindowBridge bridge;
  private readonly PreviewCaptureStabilityTracker captureStabilityTracker = new(RequiredCaptureStableFrames, MaximumCaptureObservationFrames);
  private bool disposed;

  internal DalaMockHostedPluginWindowPreviewBackend(HostedPreviewPluginSession session)
  {
    this.session = session ?? throw new ArgumentNullException(nameof(session));
    this.bridge = new HostedPluginWindowBridge(this.session.Plugin);
    this.Status = new PluginWindowBackendStatus(PluginWindowPreviewBackendMode.DalaMockHosted, PluginWindowPreviewBackendMode.DalaMockHosted, true, true, null);
  }

  public PluginWindowBackendStatus Status { get; }

  public bool DbManagerAvailable => this.bridge.DbManagerAvailable;

  public bool CaptureFailed => this.captureStabilityTracker.CaptureFailed;

  public void Draw(PreviewWorkbenchState state)
  {
    ArgumentNullException.ThrowIfNull(state);
    this.bridge.ConfigWindowOpen = state.ConfigWindowOpen;
    if (state.ConfigWindowOpen)
    {
      this.ApplyCaptureLayout(PreviewCaptureTarget.ConfigWindow);
      this.bridge.DrawConfigWindow();
      this.EnforceCaptureLayout(PreviewCaptureTarget.ConfigWindow, this.bridge.ConfigWindowName);
    }

    state.ConfigWindowOpen = this.bridge.ConfigWindowOpen;
    this.bridge.DbManagerWindowOpen = state.DbManagerWindowOpen;
    this.ApplyCaptureLayout(PreviewCaptureTarget.DbManagerWindow);
    this.bridge.DrawDbManagerWindow();
    this.EnforceCaptureLayout(PreviewCaptureTarget.DbManagerWindow, Resources.EchoglossianDBEditor);
    state.DbManagerWindowOpen = this.bridge.DbManagerWindowOpen;
    this.bridge.TranslatorMetricsWindowOpen = state.TranslatorMetricsWindowOpen;
    this.ApplyCaptureLayout(PreviewCaptureTarget.TranslatorMetricsWindow);
    this.bridge.DrawTranslatorMetricsWindow();
    this.EnforceCaptureLayout(PreviewCaptureTarget.TranslatorMetricsWindow, Resources.TranslatorDebuggerWindowTitle);
    state.TranslatorMetricsWindowOpen = this.bridge.TranslatorMetricsWindowOpen;

    if (this.captureStabilityTracker.Target is { } captureTarget && PreviewPluginWindowHost.IsPluginWindowTarget(captureTarget))
    {
      this.captureStabilityTracker.Observe(captureTarget, this.TryGetValidCaptureBounds(captureTarget));
    }
  }

  public void BeginCapture(PreviewCaptureTarget target)
  {
    if (!PreviewPluginWindowHost.IsPluginWindowTarget(target))
    {
      throw new ArgumentException("Capture stabilization requires a plugin-window target.", nameof(target));
    }

    if (target == PreviewCaptureTarget.DbManagerWindow && !this.DbManagerAvailable)
    {
      throw new InvalidOperationException("DbManagerWindow capture requires an available preview database snapshot.");
    }

    this.captureStabilityTracker.Begin(target);
  }

  public void EndCapture() => this.captureStabilityTracker.End();

  public Rectangle? TryGetStableCrop(PreviewCaptureTarget target)
  {
    return this.captureStabilityTracker.TryGetStableBounds(target, out var bounds) && PreviewPluginWindowHost.IsCaptureBoundsValid(target, this.bridge.IsWindowVisible(target), bounds) ? bounds : null;
  }

  internal static void ValidateHostedPluginWindowBridgeForTests() => HostedPluginWindowBridge.ValidateMembers();

  public void Dispose()
  {
    if (this.disposed)
    {
      return;
    }

    this.disposed = true;
    this.session.Dispose();
  }

  private void ApplyCaptureLayout(PreviewCaptureTarget target)
  {
    if (this.captureStabilityTracker.Target != target)
    {
      return;
    }

    var size = PreviewPluginWindowHost.GetCaptureLayoutSize(target);
    ImGui.SetNextWindowPos(System.Numerics.Vector2.Zero, ImGuiCond.Always);
    ImGui.SetNextWindowSize(new System.Numerics.Vector2(size.Width, size.Height), ImGuiCond.Always);
  }

  private void EnforceCaptureLayout(PreviewCaptureTarget target, string windowName)
  {
    if (this.captureStabilityTracker.Target != target)
    {
      return;
    }

    var size = PreviewPluginWindowHost.GetCaptureLayoutSize(target);
    ImGui.SetWindowPos(windowName, System.Numerics.Vector2.Zero, ImGuiCond.Always);
    ImGui.SetWindowSize(windowName, new System.Numerics.Vector2(size.Width, size.Height), ImGuiCond.Always);
  }

  private Rectangle? TryGetValidCaptureBounds(PreviewCaptureTarget target)
  {
    var bounds = this.bridge.TryGetCrop(target);
    return PreviewPluginWindowHost.IsCaptureBoundsValid(target, this.bridge.IsWindowVisible(target), bounds) ? bounds : null;
  }
}

/// <summary>Caches private production-plugin UI members used only by the preview hosted backend.</summary>
internal sealed class HostedPluginWindowBridge
{
  private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
  private static readonly HostedPluginWindowMembers Members = HostedPluginWindowMembers.Create();
  private readonly global::Echoglossian.Echoglossian plugin;

  internal HostedPluginWindowBridge(global::Echoglossian.Echoglossian plugin)
  {
    this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
  }

  internal bool DbManagerAvailable => Members.DbEditorWindowField.GetValue(this.plugin) is not null;

  internal bool ConfigWindowOpen
  {
    get => (bool)(Members.ConfigOpenField.GetValue(this.plugin) ?? false);
    set => Members.ConfigOpenField.SetValue(this.plugin, value);
  }

  internal bool DbManagerWindowOpen
  {
    get => this.GetFieldWindowOpen(Members.DbEditorWindowField, Members.DbManagerOpenField);
    set => this.SetFieldWindowOpen(Members.DbEditorWindowField, Members.DbManagerOpenField, value);
  }

  internal bool TranslatorMetricsWindowOpen
  {
    get => this.GetPropertyWindowOpen(Members.TranslatorMetricsWindowField, Members.TranslatorMetricsOpenProperty);
    set => this.SetPropertyWindowOpen(Members.TranslatorMetricsWindowField, Members.TranslatorMetricsOpenProperty, value);
  }

  internal string ConfigWindowName => $"{Resources.ConfigWindowTitle} - Plugin Version: {this.GetConfiguration().PluginVersion}";

  internal void DrawConfigWindow() => Members.DrawConfigWindowMethod.Invoke(this.plugin, null);

  internal void DrawDbManagerWindow() => Members.DrawDbManagerWindowMethod.Invoke(this.plugin, null);

  internal void DrawTranslatorMetricsWindow() => Members.DrawTranslatorMetricsWindowMethod.Invoke(this.plugin, null);

  internal Rectangle? TryGetCrop(PreviewCaptureTarget target)
  {
    var bounds = target switch
    {
      PreviewCaptureTarget.ConfigWindow => Members.ConfigBoundsProperty.GetValue(Members.ConfigWindowRendererField.GetValue(this.plugin)),
      PreviewCaptureTarget.DbManagerWindow => Members.DbManagerBoundsProperty.GetValue(Members.DbEditorWindowField.GetValue(this.plugin)),
      PreviewCaptureTarget.TranslatorMetricsWindow => Members.TranslatorMetricsBoundsProperty.GetValue(Members.TranslatorMetricsWindowField.GetValue(this.plugin)),
      _ => null,
    };

    return bounds is RectangleF rectangle
        ? Rectangle.FromLTRB((int)MathF.Floor(rectangle.Left), (int)MathF.Floor(rectangle.Top), (int)MathF.Ceiling(rectangle.Right), (int)MathF.Ceiling(rectangle.Bottom))
        : null;
  }

  internal bool IsWindowVisible(PreviewCaptureTarget target)
  {
    return target switch
    {
      PreviewCaptureTarget.ConfigWindow => this.ConfigWindowOpen,
      PreviewCaptureTarget.DbManagerWindow => this.DbManagerWindowOpen,
      PreviewCaptureTarget.TranslatorMetricsWindow => this.TranslatorMetricsWindowOpen,
      _ => false,
    };
  }

  internal static void ValidateMembers() => Members.Validate();

  private bool GetFieldWindowOpen(FieldInfo windowField, FieldInfo openField)
  {
    var window = windowField.GetValue(this.plugin);
    return window is not null && (bool)(openField.GetValue(window) ?? false);
  }

  private bool GetPropertyWindowOpen(FieldInfo windowField, PropertyInfo openProperty)
  {
    var window = windowField.GetValue(this.plugin);
    return window is not null && (bool)(openProperty.GetValue(window) ?? false);
  }

  private void SetFieldWindowOpen(FieldInfo windowField, FieldInfo openField, bool value)
  {
    var window = windowField.GetValue(this.plugin);
    if (window is not null)
    {
      openField.SetValue(window, value);
    }
  }

  private void SetPropertyWindowOpen(FieldInfo windowField, PropertyInfo openProperty, bool value)
  {
    var window = windowField.GetValue(this.plugin);
    if (window is not null)
    {
      openProperty.SetValue(window, value);
    }
  }

  private Config GetConfiguration()
  {
    return Members.ConfigurationField.GetValue(this.plugin) as Config ?? throw new InvalidOperationException("Hosted plugin configuration is unavailable.");
  }

  /// <summary>Describes cached members resolved once for every hosted backend.</summary>
  private sealed class HostedPluginWindowMembers
  {
    private HostedPluginWindowMembers(FieldInfo configOpenField, FieldInfo configurationField, FieldInfo configWindowRendererField, FieldInfo dbEditorWindowField, FieldInfo translatorMetricsWindowField, MethodInfo drawConfigWindowMethod, MethodInfo drawDbManagerWindowMethod, MethodInfo drawTranslatorMetricsWindowMethod, PropertyInfo configBoundsProperty, FieldInfo dbManagerOpenField, PropertyInfo dbManagerBoundsProperty, PropertyInfo translatorMetricsOpenProperty, PropertyInfo translatorMetricsBoundsProperty)
    {
      this.ConfigOpenField = configOpenField;
      this.ConfigurationField = configurationField;
      this.ConfigWindowRendererField = configWindowRendererField;
      this.DbEditorWindowField = dbEditorWindowField;
      this.TranslatorMetricsWindowField = translatorMetricsWindowField;
      this.DrawConfigWindowMethod = drawConfigWindowMethod;
      this.DrawDbManagerWindowMethod = drawDbManagerWindowMethod;
      this.DrawTranslatorMetricsWindowMethod = drawTranslatorMetricsWindowMethod;
      this.ConfigBoundsProperty = configBoundsProperty;
      this.DbManagerOpenField = dbManagerOpenField;
      this.DbManagerBoundsProperty = dbManagerBoundsProperty;
      this.TranslatorMetricsOpenProperty = translatorMetricsOpenProperty;
      this.TranslatorMetricsBoundsProperty = translatorMetricsBoundsProperty;
    }

    internal FieldInfo ConfigOpenField { get; }
    internal FieldInfo ConfigurationField { get; }
    internal FieldInfo ConfigWindowRendererField { get; }
    internal FieldInfo DbEditorWindowField { get; }
    internal FieldInfo TranslatorMetricsWindowField { get; }
    internal MethodInfo DrawConfigWindowMethod { get; }
    internal MethodInfo DrawDbManagerWindowMethod { get; }
    internal MethodInfo DrawTranslatorMetricsWindowMethod { get; }
    internal PropertyInfo ConfigBoundsProperty { get; }
    internal FieldInfo DbManagerOpenField { get; }
    internal PropertyInfo DbManagerBoundsProperty { get; }
    internal PropertyInfo TranslatorMetricsOpenProperty { get; }
    internal PropertyInfo TranslatorMetricsBoundsProperty { get; }

    internal static HostedPluginWindowMembers Create()
    {
      var pluginType = typeof(global::Echoglossian.Echoglossian);
      var dbEditorWindowField = GetRequiredField(pluginType, "dbEditorWindow");
      var translatorMetricsWindowField = GetRequiredField(pluginType, "translatorMetricsWindow");
      var configWindowRendererField = GetRequiredField(pluginType, "configWindowRenderer");
      return new HostedPluginWindowMembers(
          GetRequiredField(pluginType, "config"),
          GetRequiredField(pluginType, "configuration"),
          configWindowRendererField,
          dbEditorWindowField,
          translatorMetricsWindowField,
          GetRequiredMethod(pluginType, "EchoglossianConfigUi"),
          GetRequiredMethod(pluginType, "DrawDbEditorWindow"),
          GetRequiredMethod(pluginType, "DrawTranslatorMetricsWindow"),
          GetRequiredProperty(configWindowRendererField.FieldType, "LastWindowBounds"),
          GetRequiredField(dbEditorWindowField.FieldType, "IsOpen"),
          GetRequiredProperty(dbEditorWindowField.FieldType, "LastWindowBounds"),
          GetRequiredProperty(translatorMetricsWindowField.FieldType, "IsOpen"),
          GetRequiredProperty(translatorMetricsWindowField.FieldType, "LastWindowBounds"));
    }

    internal void Validate()
    {
    }

    private static FieldInfo GetRequiredField(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException($"Hosted plugin window bridge could not resolve field '{name}'.");

    private static MethodInfo GetRequiredMethod(Type type, string name) => type.GetMethod(name, InstanceNonPublic) ?? throw new InvalidOperationException($"Hosted plugin window bridge could not resolve method '{name}'.");

    private static PropertyInfo GetRequiredProperty(Type type, string name) => type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException($"Hosted plugin window bridge could not resolve property '{name}'.");
  }
}
