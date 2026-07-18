namespace DalaMock.Core.Mocks.DalamudServices;

public class MockTargetManager : ITargetManager, IMockService
{
    public nint Address { get; }

    public string ServiceName => "Target Manager";

    public IGameObject? Target { get; set; }

    public IGameObject? MouseOverTarget { get; set; }

    public IGameObject? FocusTarget { get; set; }

    public IGameObject? PreviousTarget { get; set; }

    public IGameObject? SoftTarget { get; set; }

    public IGameObject? GPoseTarget { get; set; }

    public IGameObject? MouseOverNameplateTarget { get; set; }
}
