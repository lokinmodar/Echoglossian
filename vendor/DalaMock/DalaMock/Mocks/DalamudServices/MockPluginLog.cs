namespace DalaMock.Core.Mocks.DalamudServices;

public class MockPluginLog : IPluginLog, IMockService
{
    public Serilogger Logger { get; }

    public MockPluginLog(Serilogger logger)
    {
        this.Logger = logger;
    }

    public string ServiceName => "Plugin Log";

    public void Fatal(string messageTemplate, params object[] values)
    {
        if (messageTemplate.Contains("Evicting"))
        {
            return;
        }

        this.Logger.Fatal(messageTemplate, values);
    }

    public void Fatal(Exception? exception, string messageTemplate, params object[] values)
    {
        if (messageTemplate.Contains("Evicting"))
        {
            return;
        }

        this.Logger.Fatal(exception, messageTemplate, values);
    }

    public void Error(string messageTemplate, params object[] values)
    {
        if (messageTemplate.Contains("Evicting"))
        {
            return;
        }

        this.Logger.Error(messageTemplate, values);
    }

    public void Error(Exception? exception, string messageTemplate, params object[] values)
    {
        if (messageTemplate.Contains("Evicting"))
        {
            return;
        }

        this.Logger.Error(exception, messageTemplate, values);
    }

    public void Warning(string messageTemplate, params object[] values)
    {
        if (messageTemplate.Contains("Evicting"))
        {
            return;
        }

        this.Logger.Warning(messageTemplate, values);
    }

    public void Warning(Exception? exception, string messageTemplate, params object[] values)
    {
        if (messageTemplate.Contains("Evicting"))
        {
            return;
        }

        this.Logger.Warning(exception, messageTemplate, values);
    }

    public void Information(string messageTemplate, params object[] values)
    {
        if (messageTemplate.Contains("Evicting"))
        {
            return;
        }

        this.Logger.Information(messageTemplate, values);
    }

    public void Information(Exception? exception, string messageTemplate, params object[] values)
    {
        if (messageTemplate.Contains("Evicting"))
        {
            return;
        }

        this.Logger.Information(exception, messageTemplate, values);
    }

    public void Info(string messageTemplate, params object[] values)
    {
        if (messageTemplate.Contains("Evicting"))
        {
            return;
        }

        this.Logger.Information(messageTemplate, values);
    }

    public void Info(Exception? exception, string messageTemplate, params object[] values)
    {
        if (messageTemplate.Contains("Evicting"))
        {
            return;
        }

        this.Logger.Information(exception, messageTemplate, values);
    }

    public void Debug(string messageTemplate, params object[] values)
    {
        if (messageTemplate.Contains("Evicting"))
        {
            return;
        }

        this.Logger.Debug(messageTemplate, values);
    }

    public void Debug(Exception? exception, string messageTemplate, params object[] values)
    {
        if (messageTemplate.Contains("Evicting"))
        {
            return;
        }

        this.Logger.Debug(exception, messageTemplate, values);
    }

    public void Verbose(string messageTemplate, params object[] values)
    {
        if (messageTemplate.Contains("Evicting"))
        {
            return;
        }

        this.Logger.Verbose(messageTemplate, values);
    }

    public void Verbose(Exception? exception, string messageTemplate, params object[] values)
    {
        if (messageTemplate.Contains("Evicting"))
        {
            return;
        }

        this.Logger.Verbose(exception, messageTemplate, values);
    }

    public void Write(LogEventLevel level, Exception? exception, string messageTemplate, params object[] values)
    {
        if (messageTemplate.Contains("Evicting"))
        {
            return;
        }

        this.Logger.Write(level, exception, messageTemplate, values);
    }

    public LogEventLevel MinimumLogLevel { get; set; } = LogEventLevel.Verbose;
}
