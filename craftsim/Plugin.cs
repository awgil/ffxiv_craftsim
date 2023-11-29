using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace craftsim;

public sealed class Plugin : IDalamudPlugin
{
    public DalamudPluginInterface Dalamud { get; init; }
    private ICommandManager _commandManager { get; init; }

    public WindowSystem WindowSystem = new("craftsim");
    private MainWindow _wndMain;

    public Plugin(DalamudPluginInterface dalamud, ICommandManager commandManager)
    {
        Dalamud = dalamud;
        Dalamud.Create<Service>();

        Service.Config.LoadFromFile(dalamud.ConfigFile);
        Service.SaveConfig = () => Service.Config.SaveToFile(dalamud.ConfigFile);

        _commandManager = commandManager;

        _wndMain = new MainWindow();
        _wndMain.IsOpen = true;
        WindowSystem.AddWindow(_wndMain);

        Dalamud.UiBuilder.Draw += WindowSystem.Draw;
        Dalamud.UiBuilder.OpenConfigUi += () => _wndMain.IsOpen = true;

        _commandManager.AddHandler("/csim", new CommandInfo((_, _) => _wndMain.IsOpen = true));
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        _commandManager.RemoveHandler("/csim");
        _wndMain.Dispose();
    }
}
