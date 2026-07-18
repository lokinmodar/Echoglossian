namespace DalaMock.Core.Mocks.DalamudServices;

/// <summary>
/// Provides a mock version of dalamuds key state.
/// </summary>
public class MockKeyState : IKeyState, IDisposable, IMockService
{
    private const int MaxKeyCode = 0xF0;
    private readonly ISdl2Window window;
    private readonly HashSet<VirtualKey> activeKeys = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MockKeyState"/> class.
    /// </summary>
    /// <param name="window">The mock SDL2 window.</param>
    public MockKeyState(ISdl2Window window)
    {
        this.window = window;
        this.window.KeyDown += this.WindowOnKeyDown;
        this.window.KeyUp += this.WindowOnKeyUp;
    }

    /// <inheritdoc/>
    public string ServiceName => "Key State";

    /// <inheritdoc/>
    public bool this[VirtualKey vkCode]
    {
        get => this[(int)vkCode];
        set => this[(int)vkCode] = value;
    }

    /// <inheritdoc/>
    public bool this[int vkCode]
    {
        get => this.activeKeys.Contains((VirtualKey)vkCode);
        set
        {
            if (value)
            {
                this.activeKeys.Add((VirtualKey)vkCode);
            }
            else
            {
                this.activeKeys.Remove((VirtualKey)vkCode);
            }
        }
    }

    /// <inheritdoc/>
    public int GetRawValue(int vkCode)
    {
        return 0;
    }

    /// <inheritdoc/>
    public int GetRawValue(VirtualKey vkCode)
    {
        return 0;
    }

    /// <inheritdoc/>
    public void SetRawValue(int vkCode, int value)
    {
    }

    /// <inheritdoc/>
    public void SetRawValue(VirtualKey vkCode, int value)
    {
    }

    /// <inheritdoc/>
    public bool IsVirtualKeyValid(int vkCode)
        => this.ConvertVirtualKey(vkCode) != 0;

    /// <inheritdoc/>
    public bool IsVirtualKeyValid(VirtualKey vkCode)
        => this.IsVirtualKeyValid((int)vkCode);

    /// <inheritdoc/>
    public IEnumerable<VirtualKey> GetValidVirtualKeys() => (VirtualKey[])Enum.GetValuesAsUnderlyingType<VirtualKey>();

    /// <inheritdoc/>
    public void ClearAll()
    {
        this.activeKeys.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.window.KeyDown -= this.WindowOnKeyDown;
    }

    private byte ConvertVirtualKey(int vkCode)
    {
        if (vkCode <= 0 || vkCode >= MaxKeyCode)
        {
            return 0;
        }

        return (byte)vkCode;
    }

    private void WindowOnKeyUp(KeyEvent keyEvent)
    {
        var keyState = keyEvent.ToKeyState();
        this[keyState] = false;
        if (keyEvent.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            this[VirtualKey.SHIFT] = false;
        }

        if (keyEvent.Modifiers.HasFlag(ModifierKeys.Control))
        {
            this[VirtualKey.CONTROL] = false;
        }

        if (keyEvent.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            this[VirtualKey.MENU] = false;
        }
    }

    private void WindowOnKeyDown(KeyEvent keyEvent)
    {
        var keyState = keyEvent.ToKeyState();
        this[keyState] = true;
        if (keyEvent.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            this[VirtualKey.SHIFT] = true;
        }

        if (keyEvent.Modifiers.HasFlag(ModifierKeys.Control))
        {
            this[VirtualKey.CONTROL] = true;
        }

        if (keyEvent.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            this[VirtualKey.MENU] = true;
        }
    }
}
