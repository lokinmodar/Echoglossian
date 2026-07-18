namespace DalaMock.Core.Fonts.Chooser;

using DalaMock.Shared.Interfaces;

/// <summary>
/// Creates <see cref="MockSingleFontChooserDialog"/> instances, injecting the active mock
/// <see cref="IUiBuilder"/>, configuration and system-font provider.
/// </summary>
public class MockFontChooserFactory : IFontChooserFactory
{
    private readonly IUiBuilder uiBuilder;
    private readonly MockDalamudConfiguration config;
    private readonly MockSystemFontProvider systemFontProvider;

    public MockFontChooserFactory(
        IUiBuilder uiBuilder,
        MockDalamudConfiguration config,
        MockSystemFontProvider systemFontProvider)
    {
        this.uiBuilder = uiBuilder;
        this.config = config;
        this.systemFontProvider = systemFontProvider;
    }

    /// <inheritdoc/>
    public IFontChooserDialog Create() =>
        new MockSingleFontChooserDialog(this.uiBuilder, this.config, this.systemFontProvider);

    /// <inheritdoc/>
    public IFontChooserDialog CreateAuto()
    {
        var dialog = new MockSingleFontChooserDialog(this.uiBuilder, this.config, this.systemFontProvider);

        void Draw() => dialog.Draw();

        this.uiBuilder.Draw += Draw;
        dialog.ResultTask.ContinueWith(_ =>
        {
            this.uiBuilder.Draw -= Draw;
            dialog.Dispose();
        });

        return dialog;
    }
}
