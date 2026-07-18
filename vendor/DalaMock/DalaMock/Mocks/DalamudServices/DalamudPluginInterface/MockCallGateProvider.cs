namespace DalaMock.Core.Mocks.DalamudServices.DalamudPluginInterface;

public class MockCallGateProvider<TRet> : MockCallGatePubSubBase, ICallGateProvider<TRet>
{
    public MockCallGateProvider(string name)
        : base(name)
    {
    }

    public void RegisterAction(SysAction sysAction)
    {
        base.RegisterAction(sysAction);
    }

    public void RegisterFunc(Func<TRet> func)
    {
        base.RegisterFunc(func);
    }

    public void SendMessage()
    {
    }

    public IpcContext? GetContext()
    {
        return new IpcContext();
    }

    public int SubscriptionCount { get; set; }
}

public class MockCallGateProvider<T1, TRet> : MockCallGatePubSubBase, ICallGateProvider<T1, TRet>
{
    public MockCallGateProvider(string name)
        : base(name)
    {
    }

    public void RegisterAction(Action<T1> action)
    {
        base.RegisterAction(action);
    }

    public void RegisterFunc(Func<T1, TRet> func)
    {
        base.RegisterFunc(func);
    }

    public void SendMessage(T1 arg1)
    {
    }

    public IpcContext? GetContext()
    {
        return new IpcContext();
    }

    public int SubscriptionCount { get; set; }
}

public class MockCallGateProvider<T1, T2, TRet> : MockCallGatePubSubBase, ICallGateProvider<T1, T2, TRet>
{
    public MockCallGateProvider(string name)
        : base(name)
    {
    }

    public void RegisterAction(Action<T1, T2> action)
    {
        base.RegisterAction(action);
    }

    public void RegisterFunc(Func<T1, T2, TRet> func)
    {
        base.RegisterFunc(func);
    }

    public void SendMessage(T1 arg1, T2 arg2)
    {
    }

    public IpcContext? GetContext()
    {
        return new IpcContext();
    }

    public int SubscriptionCount { get; set; }
}

public class MockCallGateProvider<T1, T2, T3, TRet> : MockCallGatePubSubBase, ICallGateProvider<T1, T2, T3, TRet>
{
    public MockCallGateProvider(string name)
        : base(name)
    {
    }

    public void RegisterAction(Action<T1, T2, T3> action)
    {
        base.RegisterAction(action);
    }

    public void RegisterFunc(Func<T1, T2, T3, TRet> func)
    {
        base.RegisterFunc(func);
    }

    public void SendMessage(T1 arg1, T2 arg2, T3 arg3)
    {
    }

    public IpcContext? GetContext()
    {
        return new IpcContext();
    }

    public int SubscriptionCount { get; set; }
}

public class MockCallGateProvider<T1, T2, T3, T4, TRet> : MockCallGatePubSubBase,
    ICallGateProvider<T1, T2, T3, T4, TRet>
{
    public MockCallGateProvider(string name)
        : base(name)
    {
    }

    public void RegisterAction(Action<T1, T2, T3, T4> action)
    {
        base.RegisterAction(action);
    }

    public void RegisterFunc(Func<T1, T2, T3, T4, TRet> func)
    {
        base.RegisterFunc(func);
    }

    public void SendMessage(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
    }

    public IpcContext? GetContext()
    {
        return new IpcContext();
    }

    public int SubscriptionCount { get; set; }
}

public class MockCallGateProvider<T1, T2, T3, T4, T5, TRet> : MockCallGatePubSubBase,
    ICallGateProvider<T1, T2, T3, T4, T5, TRet>
{
    public MockCallGateProvider(string name)
        : base(name)
    {
    }

    public void RegisterAction(Action<T1, T2, T3, T4, T5> action)
    {
        base.RegisterAction(action);
    }

    public void RegisterFunc(Func<T1, T2, T3, T4, T5, TRet> func)
    {
        base.RegisterFunc(func);
    }

    public void SendMessage(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
    }

    public IpcContext? GetContext()
    {
        return new IpcContext();
    }

    public int SubscriptionCount { get; set; }
}

public class MockCallGateProvider<T1, T2, T3, T4, T5, T6, TRet> : MockCallGatePubSubBase,
    ICallGateProvider<T1, T2, T3, T4, T5, T6, TRet>
{
    public MockCallGateProvider(string name)
        : base(name)
    {
    }

    public void RegisterAction(Action<T1, T2, T3, T4, T5, T6> action)
    {
        base.RegisterAction(action);
    }

    public void RegisterFunc(Func<T1, T2, T3, T4, T5, T6, TRet> func)
    {
        base.RegisterFunc(func);
    }

    public void SendMessage(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
    }

    public IpcContext? GetContext()
    {
        return new IpcContext();
    }

    public int SubscriptionCount { get; set; }
}

public class MockCallGateProvider<T1, T2, T3, T4, T5, T6, T7, TRet> : MockCallGatePubSubBase,
    ICallGateProvider<T1, T2, T3, T4, T5, T6, T7, TRet>
{
    public MockCallGateProvider(string name)
        : base(name)
    {
    }

    public void RegisterAction(Action<T1, T2, T3, T4, T5, T6, T7> action)
    {
        base.RegisterAction(action);
    }

    public void RegisterFunc(Func<T1, T2, T3, T4, T5, T6, T7, TRet> func)
    {
        base.RegisterFunc(func);
    }

    public void SendMessage(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
    }

    public IpcContext? GetContext()
    {
        return new IpcContext();
    }

    public int SubscriptionCount { get; set; }
}

public class MockCallGateProvider<T1, T2, T3, T4, T5, T6, T7, T8, TRet> : MockCallGatePubSubBase,
    ICallGateProvider<T1, T2, T3, T4, T5, T6, T7, T8, TRet>
{
    public MockCallGateProvider(string name)
        : base(name)
    {
    }

    public void RegisterAction(Action<T1, T2, T3, T4, T5, T6, T7, T8> action)
    {
        base.RegisterAction(action);
    }

    public void RegisterFunc(Func<T1, T2, T3, T4, T5, T6, T7, T8, TRet> func)
    {
        base.RegisterFunc(func);
    }

    public void SendMessage(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
    }

    public IpcContext? GetContext()
    {
        return new IpcContext();
    }

    public int SubscriptionCount { get; set; }
}
