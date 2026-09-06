using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using TurboController.Windows;

namespace TurboController;

public sealed class TurboControllerPlugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInterop { get; private set; } = null!;

    private const string ProbeCommandName = "/turboprobe";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("TurboController");
    private ConfigWindow ConfigWindow { get; init; }

    private readonly InjectionProbe probe = new();
    private readonly CrossbarHookProbe hookProbe;

    public TurboControllerPlugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        hookProbe = new CrossbarHookProbe();

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        // Tell the UI system that we want our windows to be drawn through the window system
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        Framework.Update += probe.OnUpdate;
        CommandManager.AddHandler(ProbeCommandName, new CommandInfo(OnProbeCommand)
        {
            HelpMessage = "Toggle the injection probe. Subcommands: copy | changed | repeat | status | sig | hook | hookinject | button <name> | inject <name|none>."
        });

        Log.Information("[TurboController] initialised");
    }

    public void Dispose()
    {
        Framework.Update -= probe.OnUpdate;
        hookProbe.Dispose();
        CommandManager.RemoveHandler(ProbeCommandName);

        // Unregister all actions to not leak anything during disposal of plugin
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
    }

    private void OnProbeCommand(string command, string args)
    {
        var parts = args.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var verb = parts.Length > 0 ? parts[0] : string.Empty;

        if (verb.Equals("copy", StringComparison.OrdinalIgnoreCase))
        {
            probe.UseSecondCopy = !probe.UseSecondCopy;
            Log.Information($"[TurboController] probe {probe.Describe()}");
            return;
        }

        if (verb.Equals("changed", StringComparison.OrdinalIgnoreCase))
        {
            probe.SetChangedFlag = !probe.SetChangedFlag;
            Log.Information($"[TurboController] probe {probe.Describe()}");
            return;
        }

        if (verb.Equals("repeat", StringComparison.OrdinalIgnoreCase))
        {
            probe.SetRepeat = !probe.SetRepeat;
            Log.Information($"[TurboController] probe {probe.Describe()}");
            return;
        }

        if (verb.Equals("sig", StringComparison.OrdinalIgnoreCase))
        {
            ScanCandidateSignatures();
            return;
        }

        if (verb.Equals("hook", StringComparison.OrdinalIgnoreCase))
        {
            if (!hookProbe.Available)
            {
                Log.Warning("[TurboController] hook probe unavailable: the signature did not resolve at load.");
                return;
            }

            hookProbe.Toggle();
            Log.Information($"[TurboController] probe {hookProbe.Describe()}");
            return;
        }

        if (verb.Equals("hookinject", StringComparison.OrdinalIgnoreCase))
        {
            hookProbe.Injecting = !hookProbe.Injecting;
            Log.Information($"[TurboController] probe {hookProbe.Describe()}");
            return;
        }

        if (verb.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            Log.Information($"[TurboController] probe {probe.Describe()}");
            Log.Information($"[TurboController] probe {hookProbe.Describe()}");
            return;
        }

        if (verb.Equals("button", StringComparison.OrdinalIgnoreCase) ||
            verb.Equals("inject", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length >= 2 && parts[1].Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                probe.Inject = null;
                Log.Information($"[TurboController] probe {probe.Describe()}");
                return;
            }

            if (parts.Length < 2 || !Enum.TryParse<GamepadButtonsFlags>(parts[1].Trim(), true, out var button))
            {
                Log.Information($"[TurboController] probe {verb}: unknown value. Valid: {string.Join(", ", Enum.GetNames<GamepadButtonsFlags>())}");
                return;
            }

            if (verb.Equals("inject", StringComparison.OrdinalIgnoreCase))
                probe.Inject = button;
            else
                probe.Watch = button;

            Log.Information($"[TurboController] probe {probe.Describe()}");
            return;
        }

        probe.Active = !probe.Active;
        Log.Information($"[TurboController] probe {probe.Describe()}");
    }

    /// <summary>
    /// THROWAWAY. Resolve-only: reports whether candidate signatures still match this
    /// game build. Nothing is hooked. Removed with the probe in Task 7.
    /// </summary>
    private static void ScanCandidateSignatures()
    {
        // name, signature, why we care
        (string Name, string Sig, string Note)[] candidates =
        {
            ("ExecuteSlot (control)",     "E9 ?? ?? ?? ?? 73 25",
                "from installed ClientStructs; if THIS fails, the scanner or the game build is the problem"),
            ("CheckCrossbarBindings",     "E8 ?? ?? ?? ?? EB 20 E8 ?? ?? ?? ?? 84 C0",
                "ReAction dead code, spec rates low confidence"),
            ("CheckHotbarBindings",       "E8 ?? ?? ?? ?? EB 07 E8 ?? ?? ?? ?? 84 C0",
                "speculative sibling of the above"),
        };

        var text = SigScanner.TextSectionBase;
        foreach (var (name, sig, note) in candidates)
        {
            try
            {
                if (SigScanner.TryScanText(sig, out var addr))
                    Log.Information($"[TurboController] sig OK   {name}: 0x{addr:X} (text+0x{addr - text:X})");
                else
                    Log.Warning($"[TurboController] sig FAIL {name}: no match — {note}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[TurboController] sig FAIL {name}: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
}
