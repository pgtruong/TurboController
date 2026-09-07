using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace TurboController.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(TurboControllerPlugin plugin) : base("Turbo Controller")
    {
        configuration = plugin.Configuration;
        Size = new Vector2(380, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    /// <summary>
    /// Helper function for the sliders, mainly to let people know you can edit with ctrl + click.
    /// </summary>
    private static void SliderHint(string? description = null)
    {
        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        if (description != null)
            ImGui.TextUnformatted(description);
        ImGui.TextUnformatted("Ctrl+Click to type an exact value.");
        ImGui.EndTooltip();
    }

    // Main Imgui logic for the config window.
    public override void Draw()
    {
        var settings = configuration.Settings;

        if (!TurboControllerPlugin.HooksReady)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Unsupported game version.");
            ImGui.TextWrapped(
                "TurboController could not locate the game functions it needs, " +
                "so turbo is inactive. This usually means the game was patched. " +
                "Check /xllog for details.");
            ImGui.Separator();
        }

        var enabled = settings.Enabled;
        if (ImGui.Checkbox("Enable hold to cast", ref enabled))
        {
            settings.Enabled = enabled;
            configuration.Save();
        }

        var interval = settings.IntervalMs;
        if (ImGui.SliderInt("Repeat interval (ms)", ref interval, TurboDecision.MinIntervalMs, 1000, "%d", ImGuiSliderFlags.AlwaysClamp))
            settings.IntervalMs = Math.Max(TurboDecision.MinIntervalMs, interval);
        // Persist on release, not on every frame of the drag: the callback fires on
        // each changed frame, which would write the config file dozens of times.
        if (ImGui.IsItemDeactivatedAfterEdit())
            configuration.Save();
        SliderHint();

        var jitter = settings.JitterMs;
        if (ImGui.SliderInt("Repeat variance (+/- ms)", ref jitter, 0, 200, "%d", ImGuiSliderFlags.AlwaysClamp))
            settings.JitterMs = Math.Max(0, jitter);
        if (ImGui.IsItemDeactivatedAfterEdit())
            configuration.Save();
        SliderHint();

        var initialDelay = settings.InitialDelayMs;
        if (ImGui.SliderInt("Initial delay (ms)", ref initialDelay, 0, 1000, "%d", ImGuiSliderFlags.AlwaysClamp))
            settings.InitialDelayMs = Math.Max(0, initialDelay);
        if (ImGui.IsItemDeactivatedAfterEdit())
            configuration.Save();
        SliderHint("Gap between your real press and the first repeat. 0 uses the repeat interval.");

        ImGui.Separator();
        ImGui.TextUnformatted("Repeat which actions");

        var gcds = settings.TurboGcds;
        if (ImGui.Checkbox("GCDs", ref gcds))
        {
            settings.TurboGcds = gcds;
            configuration.Save();
        }

        var ogcds = settings.TurboOgcds;
        if (ImGui.Checkbox("Off-GCD abilities", ref ogcds))
        {
            settings.TurboOgcds = ogcds;
            configuration.Save();
        }

        var outOfCombat = settings.TurboOutOfCombat;
        if (ImGui.Checkbox("Repeat out of combat", ref outOfCombat))
        {
            settings.TurboOutOfCombat = outOfCombat;
            configuration.Save();
        }
    }
}
