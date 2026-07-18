namespace DalaMock.Core.Mocks.DalamudServices;

public class MockNamePlateGui : INamePlateGui, IMockService
{
    public void RequestRedraw()
    {
    }

    public event INamePlateGui.OnPlateUpdateDelegate? OnNamePlateUpdate;

    public event INamePlateGui.OnPlateUpdateDelegate? OnPostNamePlateUpdate;

    public event INamePlateGui.OnPlateUpdateDelegate? OnDataUpdate;

    public event INamePlateGui.OnPlateUpdateDelegate? OnPostDataUpdate;

    public string ServiceName => "Nameplate GUI";
}
