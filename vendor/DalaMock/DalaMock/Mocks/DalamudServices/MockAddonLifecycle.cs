namespace DalaMock.Core.Mocks.DalamudServices;

public class MockAddonLifecycle : IAddonLifecycle, IMockService
{
    public void RegisterListener(
        AddonEvent eventType,
        IEnumerable<string> addonNames,
        IAddonLifecycle.AddonEventDelegate handler)
    {
    }

    public void RegisterListener(AddonEvent eventType, string addonName, IAddonLifecycle.AddonEventDelegate handler)
    {
    }

    public void RegisterListener(AddonEvent eventType, IAddonLifecycle.AddonEventDelegate handler)
    {
    }

    public void UnregisterListener(
        AddonEvent eventType,
        IEnumerable<string> addonNames,
        IAddonLifecycle.AddonEventDelegate? handler = null)
    {
    }

    public void UnregisterListener(
        AddonEvent eventType,
        string addonName,
        IAddonLifecycle.AddonEventDelegate? handler = null)
    {
    }

    public void UnregisterListener(AddonEvent eventType, IAddonLifecycle.AddonEventDelegate? handler = null)
    {
    }

    public void UnregisterListener(params IAddonLifecycle.AddonEventDelegate[] handlers)
    {
    }

    public IntPtr GetOriginalVirtualTable(IntPtr virtualTableAddress)
    {
        return IntPtr.Zero;
    }

    public string ServiceName => "Addon Lifecycle";
}
