using Dalamud.Configuration;
using Dalamud.Plugin;

namespace TurboController;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    /// <summary>
    /// Dedicated settings class that gets thrown into the main config here.
    /// Primarily separate since it doesn't make actual Dalamud references
    /// and can be unit tested properly instead of requiring in-game testing.
    /// </summary>
    public TurboSettings Settings { get; set; } = new();

    private IDalamudPluginInterface pluginInterface = null!;

    public void Initialise(IDalamudPluginInterface pluginInterface) =>
        this.pluginInterface = pluginInterface;

    public void Save() => pluginInterface.SavePluginConfig(this);
}
