namespace DalaMock.Core.Mocks.MockServices;

public class NullKeyState : IKeyState, IMockService
{
    public int GetRawValue(int vkCode)
    {
        return 0;
    }

    public int GetRawValue(VirtualKey vkCode)
    {
        return 0;
    }

    public void SetRawValue(int vkCode, int value)
    {
    }

    public void SetRawValue(VirtualKey vkCode, int value)
    {
    }

    public bool IsVirtualKeyValid(int vkCode)
    {
        return true;
    }

    public bool IsVirtualKeyValid(VirtualKey vkCode)
    {
        return true;
    }

    public bool IsExtendedVirtualKeyValid(int vkCode)
    {
        return false;
    }

    public bool IsExtendedVirtualKeyValid(VirtualKey vkCode)
    {
        return false;
    }

    public bool TryGetSeVirtualKey(int vkCode, out int seVkCode)
    {
        seVkCode = vkCode;
        return true;
    }

    public bool TryGetSeVirtualKey(VirtualKey vkCode, out int seVkCode)
    {
        return this.TryGetSeVirtualKey((int)vkCode, out seVkCode);
    }

    public IEnumerable<VirtualKey> GetValidVirtualKeys()
    {
        return new List<VirtualKey>();
    }

    public IEnumerable<VirtualKey> GetExtendedVirtualKeys()
    {
        return new List<VirtualKey>();
    }

    public void ClearAll()
    {
    }

    public bool this[int vkCode]
    {
        get => false;
        set
        {
        }
    }

    public bool this[VirtualKey vkCode]
    {
        get => false;
        set
        {
        }
    }

    public string ServiceName { get; set; } = "Key State";
}
