namespace DalaMock.Core.Mocks.DalamudServices;

public class MockGameInteropProvider : IGameInteropProvider, IMockService
{
    public void InitializeFromAttributes(object self)
    {
    }

    public Hook<T> HookFromFunctionPointerVariable<T>(nint address, T detour)
        where T : Delegate
    {
        return null!;
    }

    public Hook<T> HookFromImport<T>(
        ProcessModule? module,
        string moduleName,
        string functionName,
        uint hintOrOrdinal,
        T detour)
        where T : Delegate
    {
        return null!;
    }

    public Hook<T> HookFromSymbol<T>(
        string moduleName,
        string exportName,
        T detour,
        IGameInteropProvider.HookBackend backend = IGameInteropProvider.HookBackend.Automatic)
        where T : Delegate
    {
        return null!;
    }

    public Hook<T> HookFromAddress<T>(
        nint procAddress,
        T detour,
        IGameInteropProvider.HookBackend backend = IGameInteropProvider.HookBackend.Automatic)
        where T : Delegate
    {
        return null!;
    }

    public Hook<T> HookFromAddress<T>(
        UIntPtr procAddress,
        T detour,
        IGameInteropProvider.HookBackend backend = IGameInteropProvider.HookBackend.Automatic)
        where T : Delegate
    {
        throw new NotImplementedException();
    }

    public unsafe Hook<T> HookFromAddress<T>(
        void* procAddress,
        T detour,
        IGameInteropProvider.HookBackend backend = IGameInteropProvider.HookBackend.Automatic)
        where T : Delegate
    {
        throw new NotImplementedException();
    }

    public Hook<T> HookFromSignature<T>(
        string signature,
        T detour,
        IGameInteropProvider.HookBackend backend = IGameInteropProvider.HookBackend.Automatic)
        where T : Delegate
    {
        return null!;
    }

    public string ServiceName => "Game Interop Provider";
}
