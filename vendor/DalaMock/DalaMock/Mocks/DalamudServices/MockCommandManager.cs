namespace DalaMock.Core.Mocks.DalamudServices;

using IPluginLog = Dalamud.Plugin.Services.IPluginLog;
using IReadOnlyCommandInfo = Dalamud.Game.Command.IReadOnlyCommandInfo;

public class MockCommandManager : ICommandManager, IMockService
{
    private readonly ClientLanguage clientLanguage;
    private readonly ConcurrentDictionary<string, CommandInfo> commandMap = new();

    private readonly Regex commandRegexCn = new(
        "^^(“|「)(?<command>.+)(”|」)(出现问题：该命令不存在|出現問題：該命令不存在)。$",
        RegexOptions.Compiled);

    private readonly Regex commandRegexDe = new(
        "^„(?<command>.+)“ existiert nicht als Textkommando\\.$",
        RegexOptions.Compiled);

    private readonly Regex commandRegexEn = new(
        "^The command (?<command>.+) does not exist\\.$",
        RegexOptions.Compiled);

    private readonly Regex commandRegexFr = new(
        "^La commande texte “(?<command>.+)” n'existe pas\\.$",
        RegexOptions.Compiled);

    private readonly Regex commandRegexJp = new("^そのコマンドはありません。： (?<command>.+)$", RegexOptions.Compiled);
    private readonly Regex currentLangCommandRegex;

    private readonly IPluginLog logger;
    private ReadOnlyDictionary<string, IReadOnlyCommandInfo> commands;

    public MockCommandManager(IPluginLog logger, MockDalamudConfiguration configuration)
    {
        this.logger = logger;
        this.clientLanguage = configuration.ClientLanguage;
        Regex regex;
        switch (this.clientLanguage)
        {
            case ClientLanguage.Japanese:
                regex = this.commandRegexJp;
                break;
            case ClientLanguage.English:
                regex = this.commandRegexEn;
                break;
            case ClientLanguage.German:
                regex = this.commandRegexDe;
                break;
            case ClientLanguage.French:
                regex = this.commandRegexFr;
                break;
            default:
                throw new Exception("Not a supported language.");
                break;
        }

        this.currentLangCommandRegex = regex;
    }

    public ReadOnlyDictionary<string, CommandInfo> Commands => new(this.commandMap);

    public void DispatchCommand(string command, string argument, IReadOnlyCommandInfo info)
    {
    }

    public bool AddHandler(string command, CommandInfo info)
    {
        if (info == null)
        {
            throw new ArgumentNullException(nameof(info), "Command handler is null.");
        }

        if (this.commandMap.TryAdd(command, info))
        {
            return true;
        }

        this.logger.Error("Command {CommandName} is already registered.", command);
        return false;
    }

    public bool RemoveHandler(string command)
    {
        return this.commandMap.Remove(command, out var _);
    }

    ReadOnlyDictionary<string, IReadOnlyCommandInfo> ICommandManager.Commands => this.commands;

    public bool ProcessCommand(string content)
    {
        var length = content.IndexOf(' ');
        string str1;
        string str2;
        if (length == -1 || length + 1 >= content.Length)
        {
            str1 = length + 1 < content.Length ? content : content.Substring(0, length);
            str2 = string.Empty;
        }
        else
        {
            str1 = content.Substring(0, length);
            var num = length + 1;
            var str3 = content;
            var startIndex = num;
            str2 = str3.Substring(startIndex, str3.Length - startIndex);
        }

        CommandInfo info;
        if (!this.commandMap.TryGetValue(str1, out info))
        {
            return false;
        }

        this.DispatchCommand(str1, str2, info);
        return true;
    }

    public string ServiceName => "Command Manager";

    public void DispatchCommand(string command, string argument, CommandInfo info)
    {
        try
        {
            info.Handler(command, argument);
        }
        catch (Exception ex)
        {
            this.logger.Error(
                ex,
                "Error while dispatching command {CommandName} (Argument: {Argument})",
                command,
                argument);
        }
    }
}
