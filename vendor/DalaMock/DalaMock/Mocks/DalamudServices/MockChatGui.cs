namespace DalaMock.Core.Mocks.DalamudServices;

public class MockChatGui : IChatGui, IMockService
{
    private readonly IPluginLog pluginLog;
    private IReadOnlyDictionary<(string PluginName, uint CommandId), Action<uint, SeString>> registeredLinkHandlers;

    public MockChatGui(IPluginLog pluginLog)
    {
        this.pluginLog = pluginLog;
        this.RegisteredLinkHandlers = new ReadOnlyDictionary<(string PluginName, uint CommandId), Action<uint, SeString>>(new Dictionary<(string PluginName, uint CommandId), Action<uint, SeString>>());
    }

    IReadOnlyDictionary<(string PluginName, uint CommandId), Action<uint, SeString>> IChatGui.RegisteredLinkHandlers => this.registeredLinkHandlers;

    public event IChatGui.OnHandleableChatMessageDelegate? ChatMessage;

    public event IChatGui.OnHandleableChatMessageDelegate? CheckMessageHandled;

    public event IChatGui.OnChatMessageDelegate? ChatMessageHandled;

    public event IChatGui.OnChatMessageDelegate? ChatMessageUnhandled;

    public event IChatGui.OnLogMessageDelegate? LogMessage;

    /// <inheritdoc />
    public IReadOnlyDictionary<(string PluginName, uint CommandId), Action<uint, SeString>> RegisteredLinkHandlers
    {
        get;
        set;
    }

    public void PrintError(ReadOnlySpan<byte> message, string? messageTag = null, ushort? tagColor = null)
    {
        var stringBuilder = new Lumina.Text.SeStringBuilder();
        stringBuilder.Append(message);
        this.PrintError(stringBuilder.ToSeString(), messageTag, tagColor);
    }

    /// <inheritdoc />
    public uint LastLinkedItemId { get; set; }

    /// <inheritdoc />
    public byte LastLinkedItemFlags { get; set; }

    /// <inheritdoc />
    public string ServiceName => "Chat Gui";

    public DalamudLinkPayload AddChatLinkHandler(uint commandId, Action<uint, SeString> commandAction)
    {
        throw new NotImplementedException();
    }

    public void RemoveChatLinkHandler(uint commandId)
    {
        throw new NotImplementedException();
    }

    public void RemoveChatLinkHandler()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void Print(XivChatEntry chat)
    {
        this.pluginLog.Info($"[{chat.Type.ToString()}] [{chat.Timestamp:HH:mm:ss}] {chat.Message}");
    }

    /// <inheritdoc />
    public void Print(string message, string? messageTag = null, ushort? tagColor = null)
    {
        this.pluginLog.Info($"{(messageTag == null ? string.Empty : $"[{messageTag}] ")}{message}");
    }

    /// <inheritdoc />
    public void Print(SeString message, string? messageTag = null, ushort? tagColor = null)
    {
        this.pluginLog.Info($"{(messageTag == null ? string.Empty : $"[{messageTag}] ")}{message}");
    }

    /// <inheritdoc />
    public void PrintError(string message, string? messageTag = null, ushort? tagColor = null)
    {
        this.pluginLog.Info($"ERROR: {(messageTag == null ? string.Empty : $"[{messageTag}] ")}{message}");
    }

    /// <inheritdoc />
    public void PrintError(SeString message, string? messageTag = null, ushort? tagColor = null)
    {
        this.pluginLog.Info($"ERROR: {(messageTag == null ? string.Empty : $"[{messageTag}] ")}{message.TextValue}");
    }

    public void Print(ReadOnlySpan<byte> message, string? messageTag = null, ushort? tagColor = null)
    {
        var stringBuilder = new Lumina.Text.SeStringBuilder();
        stringBuilder.Append(message);
        this.Print(stringBuilder.ToSeString(), messageTag, tagColor);
    }

    public virtual void OnChatMessage(IHandleableChatMessage message)
    {
        this.ChatMessage?.Invoke(message);
    }

    public virtual void OnCheckMessageHandled(IHandleableChatMessage message)
    {
        this.CheckMessageHandled?.Invoke(message);
    }

    public virtual void OnChatMessageHandled(IChatMessage message)
    {
        this.ChatMessageHandled?.Invoke(message);
    }

    public virtual void OnChatMessageUnhandled(IChatMessage message)
    {
        this.ChatMessageUnhandled?.Invoke(message);
    }
}
