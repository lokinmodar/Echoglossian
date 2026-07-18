namespace DalaMock.Core.Mocks.DalamudServices.DalamudPluginInterface;

public abstract class MockCallGatePubSubBase
{
    protected MockCallGatePubSubBase(string name)
    {
        this.Name = name;
    }

    public Delegate? Func { get; set; }

    public Delegate? Action { get; set; }

    public string Name { get; }

    /// <summary>
    /// Removes a registered Action from inter-plugin communication.
    /// </summary>
    public void UnregisterAction()
    {
        this.Action = null;
    }

    /// <summary>
    /// Removes a registered Func from inter-plugin communication.
    /// </summary>
    public void UnregisterFunc()
    {
        this.Func = null;
    }

    /// <summary>
    /// Registers an Action for inter-plugin communication.
    /// </summary>
    /// <param name="action">Action to register.</param>
    private protected void RegisterAction(Delegate action)
    {
        this.Action = action;
    }

    /// <summary>
    /// Registers a Func for inter-plugin communication.
    /// </summary>
    /// <param name="func">Func to register.</param>
    private protected void RegisterFunc(Delegate func)
    {
        this.Func = func;
    }

    /// <summary>
    /// Subscribe an expression to this registration.
    /// </summary>
    /// <param name="action">Action to subscribe.</param>
    private protected void Subscribe(Delegate action)
    {
    }

    /// <summary>
    /// Unsubscribe an expression from this registration.
    /// </summary>
    /// <param name="action">Action to unsubscribe.</param>
    private protected void Unsubscribe(Delegate action)
    {
    }

    /// <summary>
    /// Invoke an action registered for inter-plugin communication.
    /// </summary>
    /// <param name="args">Action arguments.</param>
    /// <exception cref="Dalamud.Plugin.Ipc.Exceptions.IpcNotReadyError">This is thrown when the IPC publisher has not registered an action for calling yet.</exception>
    private protected void InvokeAction(params object?[]? args)
    {
    }

    /// <summary>
    /// Invoke all actions that have subscribed to this IPC.
    /// </summary>
    /// <param name="args">Delegate arguments.</param>
    private protected void SendMessage(params object?[]? args)
    {
    }
}
