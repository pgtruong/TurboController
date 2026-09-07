using Dalamud.Configuration;

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

    public void Save() => TurboControllerPlugin.PluginInterface.SavePluginConfig(this);
}
