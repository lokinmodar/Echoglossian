namespace DalaMock.Core.Mocks.DalamudServices;

public class MockObjectTable : IObjectTable, IMockService
{
    public int Count { get; }

    public string ServiceName => "Object Table";

    public IEnumerator<IGameObject> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }

    public IGameObject? SearchById(ulong objectId)
    {
        throw new NotImplementedException();
    }

    public IGameObject? SearchByEntityId(uint entityId)
    {
        throw new NotImplementedException();
    }

    public nint GetObjectAddress(int index)
    {
        throw new NotImplementedException();
    }

    public IGameObject? CreateObjectReference(nint address)
    {
        throw new NotImplementedException();
    }

    public nint Address { get; }

    public int Length { get; }

    public IPlayerCharacter? LocalPlayer { get; set; }

    public IEnumerable<IBattleChara> PlayerObjects { get; set; }

    public IEnumerable<IGameObject> CharacterManagerObjects { get; set; }

    public IEnumerable<IGameObject> ClientObjects { get; set; }

    public IEnumerable<IGameObject> EventObjects { get; set; }

    public IEnumerable<IGameObject> StandObjects { get; set; }

    public IEnumerable<IGameObject> ReactionEventObjects { get; set; }

    public IGameObject? this[int index] => throw new NotImplementedException();
}
