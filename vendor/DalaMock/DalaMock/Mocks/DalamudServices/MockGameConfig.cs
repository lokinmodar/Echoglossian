namespace DalaMock.Core.Mocks.DalamudServices;

public class MockGameConfig : IGameConfig, IMockService
{
    private readonly Dictionary<SystemConfigOption, bool> systemBool = new();
    private readonly Dictionary<SystemConfigOption, uint> systemUint = new();
    private readonly Dictionary<SystemConfigOption, float> systemFloat = new();
    private readonly Dictionary<SystemConfigOption, string> systemString = new();

    private readonly Dictionary<UiConfigOption, bool> uiConfigBool = new();
    private readonly Dictionary<UiConfigOption, uint> uiConfigUint = new();
    private readonly Dictionary<UiConfigOption, float> uiConfigFloat = new();
    private readonly Dictionary<UiConfigOption, string> uiConfigString = new();

    private readonly Dictionary<UiControlOption, bool> uiControlBool = new();
    private readonly Dictionary<UiControlOption, uint> uiControlUint = new();
    private readonly Dictionary<UiControlOption, float> uiControlFloat = new();
    private readonly Dictionary<UiControlOption, string> uiControlString = new();

    public void SetMock(SystemConfigOption option, bool value)
        => this.SetAndNotify(option, () => this.systemBool[option] = value);

    public void SetMock(SystemConfigOption option, uint value)
        => this.SetAndNotify(option, () => this.systemUint[option] = value);

    public void SetMock(SystemConfigOption option, float value)
        => this.SetAndNotify(option, () => this.systemFloat[option] = value);

    public void SetMock(SystemConfigOption option, string value)
        => this.SetAndNotify(option, () => this.systemString[option] = value);

    public void SetMock(UiConfigOption option, bool value)
        => this.SetAndNotify(option, () => this.uiConfigBool[option] = value);

    public void SetMock(UiConfigOption option, uint value)
        => this.SetAndNotify(option, () => this.uiConfigUint[option] = value);

    public void SetMock(UiConfigOption option, float value)
        => this.SetAndNotify(option, () => this.uiConfigFloat[option] = value);

    public void SetMock(UiConfigOption option, string value)
        => this.SetAndNotify(option, () => this.uiConfigString[option] = value);

    public void SetMock(UiControlOption option, bool value)
        => this.SetAndNotify(option, () => this.uiControlBool[option] = value);

    public void SetMock(UiControlOption option, uint value)
        => this.SetAndNotify(option, () => this.uiControlUint[option] = value);

    public void SetMock(UiControlOption option, float value)
        => this.SetAndNotify(option, () => this.uiControlFloat[option] = value);

    public void SetMock(UiControlOption option, string value)
        => this.SetAndNotify(option, () => this.uiControlString[option] = value);

    public bool TryGet(SystemConfigOption option, out bool value)
        => this.systemBool.TryGetValue(option, out value);

    public bool TryGet(SystemConfigOption option, out uint value)
        => this.systemUint.TryGetValue(option, out value);

    public bool TryGet(SystemConfigOption option, out float value)
        => this.systemFloat.TryGetValue(option, out value);

    public bool TryGet(SystemConfigOption option, out string value)
        => this.systemString.TryGetValue(option, out value!);

    public bool TryGet(SystemConfigOption option, out UIntConfigProperties? properties)
        => TryBuildUIntProperties(this.systemUint, option, out properties);

    public bool TryGet(SystemConfigOption option, out FloatConfigProperties? properties)
        => TryBuildFloatProperties(this.systemFloat, option, out properties);

    public bool TryGet(SystemConfigOption option, out StringConfigProperties? properties)
        => TryBuildStringProperties(this.systemString, option, out properties);

    public bool TryGet(SystemConfigOption option, out PadButtonValue value)
    {
        if (this.systemString.TryGetValue(option, out var raw) &&
            Enum.TryParse(raw, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGet(UiConfigOption option, out bool value)
        => this.uiConfigBool.TryGetValue(option, out value);

    public bool TryGet(UiConfigOption option, out uint value)
        => this.uiConfigUint.TryGetValue(option, out value);

    public bool TryGet(UiConfigOption option, out float value)
        => this.uiConfigFloat.TryGetValue(option, out value);

    public bool TryGet(UiConfigOption option, out string value)
        => this.uiConfigString.TryGetValue(option, out value!);

    public bool TryGet(UiConfigOption option, out UIntConfigProperties? properties)
        => TryBuildUIntProperties(this.uiConfigUint, option, out properties);

    public bool TryGet(UiConfigOption option, out FloatConfigProperties? properties)
        => TryBuildFloatProperties(this.uiConfigFloat, option, out properties);

    public bool TryGet(UiConfigOption option, out StringConfigProperties? properties)
        => TryBuildStringProperties(this.uiConfigString, option, out properties);

    public bool TryGet(UiControlOption option, out bool value)
        => this.uiControlBool.TryGetValue(option, out value);

    public bool TryGet(UiControlOption option, out uint value)
        => this.uiControlUint.TryGetValue(option, out value);

    public bool TryGet(UiControlOption option, out float value)
        => this.uiControlFloat.TryGetValue(option, out value);

    public bool TryGet(UiControlOption option, out string value)
        => this.uiControlString.TryGetValue(option, out value!);

    public bool TryGet(UiControlOption option, out UIntConfigProperties? properties)
        => TryBuildUIntProperties(this.uiControlUint, option, out properties);

    public bool TryGet(UiControlOption option, out FloatConfigProperties? properties)
        => TryBuildFloatProperties(this.uiControlFloat, option, out properties);

    public bool TryGet(UiControlOption option, out StringConfigProperties? properties)
        => TryBuildStringProperties(this.uiControlString, option, out properties);

    public void Set(SystemConfigOption option, bool value) => this.SetMock(option, value);

    public void Set(SystemConfigOption option, uint value) => this.SetMock(option, value);

    public void Set(SystemConfigOption option, float value) => this.SetMock(option, value);

    public void Set(SystemConfigOption option, string value) => this.SetMock(option, value);

    public void Set(UiConfigOption option, bool value) => this.SetMock(option, value);

    public void Set(UiConfigOption option, uint value) => this.SetMock(option, value);

    public void Set(UiConfigOption option, float value) => this.SetMock(option, value);

    public void Set(UiConfigOption option, string value) => this.SetMock(option, value);

    public void Set(UiControlOption option, bool value) => this.SetMock(option, value);

    public void Set(UiControlOption option, uint value) => this.SetMock(option, value);

    public void Set(UiControlOption option, float value) => this.SetMock(option, value);

    public void Set(UiControlOption option, string value) => this.SetMock(option, value);

    public GameConfigSection System { get; } = null!;

    public GameConfigSection UiConfig { get; } = null!;

    public GameConfigSection UiControl { get; } = null!;

    public event EventHandler<ConfigChangeEvent>? Changed;

    public event EventHandler<ConfigChangeEvent>? SystemChanged;

    public event EventHandler<ConfigChangeEvent>? UiConfigChanged;

    public event EventHandler<ConfigChangeEvent>? UiControlChanged;

    public string ServiceName => nameof(MockGameConfig);

    private void SetAndNotify<T>(T option, SysAction store) where T : Enum
    {
        store();
        var ev = new ConfigChangeEvent<T>(option);

        this.Changed?.Invoke(this, ev);

        switch (ev)
        {
            case ConfigChangeEvent<SystemConfigOption> sys:
                this.SystemChanged?.Invoke(this, sys);
                break;
            case ConfigChangeEvent<UiConfigOption> ui:
                this.UiConfigChanged?.Invoke(this, ui);
                break;
            case ConfigChangeEvent<UiControlOption> ctrl:
                this.UiControlChanged?.Invoke(this, ctrl);
                break;
        }
    }

    private static bool TryBuildUIntProperties<TKey>(
        Dictionary<TKey, uint> store, TKey key, out UIntConfigProperties? properties)
        where TKey : notnull
    {
        if (store.TryGetValue(key, out var v))
        {
            properties = new UIntConfigProperties(0, 0, 100);
            return true;
        }
        properties = null;
        return false;
    }

    private static bool TryBuildFloatProperties<TKey>(
        Dictionary<TKey, float> store, TKey key, out FloatConfigProperties? properties)
        where TKey : notnull
    {
        if (store.TryGetValue(key, out var v))
        {
            properties = new FloatConfigProperties(0, 0, 100);
            return true;
        }
        properties = null;
        return false;
    }

    private static bool TryBuildStringProperties<TKey>(
        Dictionary<TKey, string> store, TKey key, out StringConfigProperties? properties)
        where TKey : notnull
    {
        if (store.TryGetValue(key, out var v))
        {
            properties = new StringConfigProperties(null);
            return true;
        }
        properties = null;
        return false;
    }
}
