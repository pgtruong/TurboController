using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using TurboController.Windows;

namespace TurboController;

public sealed class TurboControllerPlugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInterop { get; private set; } = null!;

    /// <summary>
    /// False when signature resolution failed, so the UI can say so.
    /// </summary>
    internal static bool HooksReady { get; set; }

    /// <summary>
    /// True when injection threw so often that it was switched off at runtime.
    /// Distinct from <see cref="HooksReady"/>, which is about load-time signature
    /// resolution.
    /// </summary>
    internal static bool InjectionFailed { get; set; }

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("TurboController");
    private ConfigWindow ConfigWindow { get; init; }

    private readonly CrossbarInjector injector;

    public TurboControllerPlugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialise(PluginInterface);

        injector = new CrossbarInjector(Configuration);

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
    }

    public void Dispose()
    {
        injector.Dispose();

        // Unregister all actions to not leak anything during disposal of plugin
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
}
