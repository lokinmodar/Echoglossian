namespace DalaMock.Core.Mocks.DalamudServices;

using ToastOptions = Dalamud.Game.Gui.Toast.ToastOptions;

public class MockToastGui : IToastGui, IMockService
{
    public string ServiceName => "Toasts";

    public void ShowNormal(string message, ToastOptions? options = null)
    {
        throw new NotImplementedException();
    }

    public void ShowNormal(SeString message, ToastOptions? options = null)
    {
        throw new NotImplementedException();
    }

    public void ShowQuest(string message, QuestToastOptions? options = null)
    {
        throw new NotImplementedException();
    }

    public void ShowQuest(SeString message, QuestToastOptions? options = null)
    {
        throw new NotImplementedException();
    }

    public void ShowError(string message)
    {
        throw new NotImplementedException();
    }

    public void ShowError(SeString message)
    {
        throw new NotImplementedException();
    }

    public event IToastGui.OnNormalToastDelegate? Toast;

    public event IToastGui.OnQuestToastDelegate? QuestToast;

    public event IToastGui.OnErrorToastDelegate? ErrorToast;
}
