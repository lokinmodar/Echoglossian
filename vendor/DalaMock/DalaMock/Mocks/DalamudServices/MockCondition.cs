namespace DalaMock.Core.Mocks.DalamudServices;

public class MockCondition : ICondition, IMockService
{
    public IReadOnlySet<ConditionFlag> AsReadOnlySet()
    {
        throw new NotImplementedException();
    }

    public bool Any()
    {
        return false;
    }

    public bool Any(params ConditionFlag[] flags)
    {
        return false;
    }

    public bool AnyExcept(params ConditionFlag[] except)
    {
        throw new NotImplementedException();
    }

    public bool OnlyAny(params ConditionFlag[] other)
    {
        throw new NotImplementedException();
    }

    public bool EqualTo(params ConditionFlag[] other)
    {
        throw new NotImplementedException();
    }

    public int MaxEntries { get; } = 0;

    public nint Address { get; } = 0;

    public bool this[int flag] => false;

    public event ICondition.ConditionChangeDelegate? ConditionChange;

    public string ServiceName => "Condition";
}
